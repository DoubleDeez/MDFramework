using System;
using System.Reflection;
using Godot;
using OwnerMap = System.Collections.Generic.Dictionary<string, int>;
using MethodMap = System.Collections.Generic.Dictionary<string, System.Reflection.MethodInfo>;
using NodeMethodMap = System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.Dictionary<string, System.Reflection.MethodInfo>>;
using NodeMap = System.Collections.Generic.Dictionary<string, System.WeakReference>;

public class MDRemoteCaller
{
    private const string LOG_CAT = "LogRemoteCaller";

    // Set a peer ID as network owner
    public void SetNetworkOwner(string NodeName, int PeerID)
    {
        // Only server can set network owner
        if (MDStatics.GetNetMode() == MDNetMode.Server)
        {
            // We only store the network owner when it's not the server/standalone client
            if (PeerID < 0)
            {
                NetworkOwners.Remove(NodeName);
            }
            else
            {
                NetworkOwners[NodeName] = PeerID;
            }
        }
    }

    // Returns true if the specified peer is the network owner of the specified node name
    public bool IsNetworkOwner(string NodeName, int PeerID)
    {
        if (PeerID < 0)
        {
            // Specified peer is server or standalone which always have owner rights
            return true;
        }

        if (NetworkOwners.ContainsKey(NodeName))
        {
            return PeerID == NetworkOwners[NodeName];
        }

        return false;
    }

    // Returns the network owner PeerID of the specified node
    public int GetNetworkOwner(string NodeName)
    {
        if (NetworkOwners.ContainsKey(NodeName))
        {
            return NetworkOwners[NodeName];
        }

        return MDStatics.GetNetMode() == MDNetMode.Server ? MDGameSession.SERVER_PEER_ID : MDGameSession.STANDALONE_PEER_ID;
    }

    // Returns true if the local peer is the network owner of the specified node name
    public bool IsNetworkOwner(string NodeName)
    {
        return IsNetworkOwner(NodeName, MDStatics.GetPeerID());
    }
    
    // Register the passed in node's rpc methods
    public void RegisterRPCs(Node Instance)
    {
        Type NodeType = Instance.GetType();
        if (!NodeMethods.ContainsKey(NodeType))
        {
            // Map function names to MethodInfo
            MethodMap Methods = new MethodMap();
            MethodInfo[] MethodInfos = NodeType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach(MethodInfo Method in MethodInfos)
            {
                MDRpc RpcAttribute = Method.GetCustomAttribute(typeof(MDRpc)) as MDRpc;
                if (RpcAttribute != null)
                {
                    Methods.Add(Method.Name, Method);
                    MDLog.CError(Method.ReturnType != typeof(void), LOG_CAT, "RPC functions can't have return values [{0}::{1}]", NodeType.Name, Method.Name);
                }
            }

            NodeMethods.Add(NodeType, Methods);
        }

        RPCNodes[Instance.GetName()] = new WeakReference(Instance);
    }

    // Call the RPC function as appropriate
    public void CallRPC(Node Instance, string FunctionName, params object[] args)
    {
        Type NodeType = Instance.GetType();
        if (!NodeMethods.ContainsKey(NodeType))
        {
            MDLog.Error(LOG_CAT, "Attempting to call RPC on non-registered node [{0}]", NodeType.Name);
            return;
        }

        MethodMap Methods = NodeMethods[NodeType];
        if (!Methods.ContainsKey(FunctionName))
        {
            MDLog.Error(LOG_CAT, "Attempting to call non-RPC function [{0}::{1}]", NodeType.Name, FunctionName);
            return;
        }

        MethodInfo RPCMethod = Methods[FunctionName];
        MDRpc RpcAttribute = RPCMethod.GetCustomAttribute(typeof(MDRpc)) as MDRpc;
        if (RpcAttribute == null)
        {
            MDLog.Error(LOG_CAT, "[{0}::{1}] is not an MDRpc", NodeType.Name, FunctionName);
            return;
        }

        // TODO - Validate arguments

        string NodeName = Instance.GetName();
        MDNetMode NetMode = MDStatics.GetNetMode();
        RPCType CallType = RpcAttribute.Type;
        RPCReliability Reliability = RpcAttribute.Reliability;
        if (CallType == RPCType.Server)
        {
            if (NetMode == MDNetMode.Client)
            {
                MDGameSession GameSession = MDStatics.GetGameSession();
                GameSession.SendPacket(MDPacketType.RPC, BuildDataArray(Instance.GetName(), FunctionName, args), Reliability);
            }
            else // We are the server (or standalone)
            {
                RPCMethod.Invoke(Instance, args);
            }
            return;
        }
        else if (NetMode != MDNetMode.Client)
        {
            if (CallType == RPCType.Client)
            {
                int NetOwner = GetNetworkOwner(NodeName);
                if (NetOwner >= 0)
                {
                    MDGameSession GameSession = MDStatics.GetGameSession();
                    GameSession.SendPacket(NetOwner, MDPacketType.RPC,  BuildDataArray(Instance.GetName(), FunctionName, args), Reliability);
                }
                else
                {
                    RPCMethod.Invoke(Instance, args);
                }
                return;
            }
            else
            {
                // Broadcast the call
                MDGameSession GameSession = MDStatics.GetGameSession();
                GameSession.BroadcastPacket(MDPacketType.RPC,  BuildDataArray(Instance.GetName(), FunctionName, args), Reliability);
                RPCMethod.Invoke(Instance, args);
                return;
            }
        }

        MDLog.Error(LOG_CAT, "Invalid RPCType [{0}] and NetMode [{1}] for function [{2}::{3}]"
            , CallType, NetMode, NodeType.Name, FunctionName);
    }

    // Called when we receive an RPC packet
    public void HandleRPCPacket(byte[] Data, int SenderID)
    {
        string NodeName;
        int BytesUsed = MDSerialization.GetStringFromStartOfByteArray(Data, out NodeName);
        
        string MethodName;
        BytesUsed += MDSerialization.GetStringFromStartOfByteArray(Data.SubArray(BytesUsed), out MethodName);

        MDLog.Info(LOG_CAT, "Received RPC call on Node [{0}] for function [{1}]", NodeName, MethodName);

        if (!RPCNodes.ContainsKey(NodeName))
        {
            MDLog.Error(LOG_CAT, "Received RPC call for unregistered Node [{0}]", NodeName);
            return;
        }

        WeakReference WeakNode = RPCNodes[NodeName];
        Node RPCNode = WeakNode.Target as Node;
        if (RPCNode == null)
        {
            // The node was destroyed
            RPCNodes.Remove(NodeName);
            return;
        }

        Type NodeType = RPCNode.GetType();
        if (!NodeMethods.ContainsKey(NodeType))
        {
            MDLog.Error(LOG_CAT, "Received RPC call for unregistered Node Type [{0}]", NodeType.Name);
            return;
        }

        MethodMap Methods = NodeMethods[NodeType];
        if (!Methods.ContainsKey(MethodName))
        {
            MDLog.Error(LOG_CAT, "Received RPC call for unregistered method [{0}::{1}]", NodeType.Name, MethodName);
            return;
        }

        MethodInfo Method = Methods[MethodName];
        MDRpc RpcAttribute = Method.GetCustomAttribute(typeof(MDRpc)) as MDRpc;
        MDNetMode NetMode = MDStatics.GetNetMode();
        RPCType CallType = RpcAttribute.Type;

        // Validate the RPC call before invoking
        if (NetMode == MDNetMode.Server)
        {
            if (CallType != RPCType.Server)
            {
                MDLog.Error(LOG_CAT, "Server received RPC function [{0}::{1}] of type {2}", NodeType.Name, MethodName, CallType);
                return;
            }

            int NetOwner = GetNetworkOwner(NodeName);
            if (NetOwner != SenderID)
            {
                MDLog.Error(LOG_CAT, "Server received RPC function [{0}::{1}] from ID: [{2}] but owner is [{3}]", NodeType.Name, MethodName, SenderID, NetOwner);
                return;
            }
        }
        else
        {
            if (CallType == RPCType.Server)
            {
                MDLog.Error(LOG_CAT, "Client received server RPC function [{0}::{1}]", NodeType.Name, MethodName);
                return;
            }
        }

        ParameterInfo[] Params = Method.GetParameters();
        object[] args = new object[Params.Length];
        if (args.Length > 0)
        {
            byte[] ArgData = Data.SubArray(BytesUsed);
            int ByteCount = ArgData.Length;
            int BytePos = 0;
            int ParamIndex = 0;
            foreach(ParameterInfo Param in Params)
            {
                if (BytePos >= ByteCount)
                {
                    MDLog.Error(LOG_CAT, "Not enough argument data to call RPC");
                    return;
                }

                object ParamObj;
                BytePos += MDSerialization.GetObjectFromStartOfByteArray(Param.ParameterType, ArgData.SubArray(BytePos), out ParamObj);
                args[ParamIndex] = ParamObj;
            }
        }

        Method.Invoke(RPCNode, args);
    }

    // Builds the data array for the rpc call
    private byte[] BuildDataArray(string NodeName, string MethodName, params object[] args)
    {
        // Maybe referencing methods by an index would be better?
        /*
            [Node name length] int
            [Node name] string
            [Method name length] int
            [Method name] string
            [Arguments] byte[]
         */
        byte[] NodeBytes = MDSerialization.ConvertSupportedTypeToBytes(NodeName);
        byte[] MethodBytes = MDSerialization.ConvertSupportedTypeToBytes(MethodName);

        return MDStatics.JoinByteArrays(NodeBytes, MethodBytes, MDSerialization.ConvertObjectsToBytes(args));
    }

    OwnerMap NetworkOwners = new OwnerMap();
    // Map node names to their WeakReference instances
    NodeMap RPCNodes = new NodeMap();
    // Map of class type to RPC functions, used to save memory rather than storing per instance
    NodeMethodMap NodeMethods = new NodeMethodMap();
}