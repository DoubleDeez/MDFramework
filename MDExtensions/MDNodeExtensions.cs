using Godot;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Extension class to provide useful framework methods
    /// </summary>
    public static class MDNodeExtensions
    {
        private const string LOG_CAT = "LogNodeExtensions";

        /// <summary>
        /// Grabs the singleton game instance, doesn't rely on being in the tree
        /// </summary>
        public static MDGameInstance GetGameInstance(this Node Instance)
        {
            return MDStatics.GetGameInstance();
        }

        /// <summary>Grabs the GameSession from the GameInstance</summary>
        public static MDGameSession GetGameSession(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSession;
        }

        /// <summary>Grabs the Configuration from the GameInstance</summary>
        public static MDConfiguration GetConfiguration(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.Configuration;
        }

        /// <summary>Grabs the GameSynchronizer from the GameInstance</summary>
        public static MDGameSynchronizer GetGameSynchronizer(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer;
        }

        /// <summary>Grabs the GameClock from the GameInstance</summary>
        public static MDGameClock GetGameClock(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameClock;
        }

        /// <summary>Grabs the Interface Manager from the GameInstance</summary>
        public static MDInterfaceManager GetInterfaceManager(this Node Instance)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.InterfaceManager;
        }

        /// <summary>Gets the ping of the given peer</summary>
        public static int GetPlayerPing(this Node Instance, int PeerId)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer.GetPlayerPing(PeerId);
        }

        /// <summary>Gets the estimated OS.GetTicksMsec of the given peer</summary>
        public static uint GetPlayerTicksMsec(this Node Instance, int PeerId)
        {
            MDGameInstance GI = Instance.GetGameInstance();
            return GI.GameSynchronizer.GetPlayerTicksMsec(PeerId);
        }

        /// <summary>Get the input state</summary>
        public static MDInput GetInputState(this Node Instance)
        {
            return Instance.GetGameInstance().InputState;
        }

        /// <summary>
        /// Get the  player info for the peer
        /// </summary>
        /// <param name="Instance">The node this is called from</param>
        /// <param name="PeerId">The peer id</param>
        /// <typeparam name="T">The type of player info you want</typeparam>
        /// <returns>The player info as T</returns>
        public static T GetPlayerInfo<T>(this Node Instance, int PeerId) where T : MDPlayerInfo
        {
            MDGameSession Session = Instance.GetGameSession();
            MDPlayerInfo PlayerInfo = Session.GetPlayerInfo(PeerId);
            return PlayerInfo as T;
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="NodeType">The type of node to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName,
            int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, true, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="NodeType">The type of node to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName, bool UseRandomName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="Scene">The packed scene to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="Scene">The packed scene to spawn</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName,
            bool UseRandomName,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Instance">The Node from where function is called</param>
        /// <param name="ScenePath">The path to the scene</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        public static Node SpawnNetworkedNode(this Node Instance, string ScenePath, string NodeName,
            int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            return GameSession.SpawnNetworkedNode(ScenePath, Instance, NodeName, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Shortcut for GetTree().GetRoot().AddChild()
        /// </summary>
        /// <param name="Instance">The instance of the node</param>
        /// <param name="Child">The child to add</param>
        /// <param name="Deferred">Should this be a deferred call</param>
        public static void AddNodeToRoot(this Node Instance, Node Child, bool Deferred = false)
        {
            if (Deferred)
            {
                Instance.GetTree().Root.CallDeferred("add_child", Child);
            }
            else
            {
                Instance.GetTree().Root.AddChild(Child);
            }
        }

        /// <summary>Helper to mark input as handled</summary>
        public static void SetInputHandled(this Node Instance)
        {
            Instance.GetTree().SetInputAsHandled();
        }

        /// <summary>Helper to register commands</summary>
        public static void RegisterCommandAttributes(this Node Instance)
        {
            MDCommands.RegisterCommandAttributes(Instance);
        }

        /// <summary>Helper to unregister commands</summary>
        public static void UnregisterCommandAttributes(this Node Instance)
        {
            MDCommands.UnregisterCommandAttributes(Instance);
        }

        /// <summary>Helper to register replication</summary>
        public static void RegisterReplicatedAttributes(this Node Instance)
        {
            Instance.GetGameSession().Replicator.RegisterReplication(Instance);
        }

        /// <summary>Helper to unregister replication</summary>
        public static void UnregisterReplicatedAttributes(this Node Instance)
        {
            Instance.GetGameSession().Replicator.UnregisterReplication(Instance);
        }

        /// <summary>Helper to populate members marked with [MDBindNode()]</summary>
        public static void PopulateBindNodes(this Node Instance)
        {
            MDBindNode.PopulateBindNodes(Instance);
        }

        /// <summary>
        /// Returns true if this application can set replicated variables, call client RPCs, and broadcast RPCs
        /// </summary>
        /// <returns>Returns true if we are server, false if not</returns>
        public static bool IsServer(this Node Instance)
        {
            return Instance.GetNetMode() < MDNetMode.Client;
        }

        /// <summary>
        /// Returns true if the local peer is the network master of the node or we're not networking
        /// </summary>
        /// <returns>True if we are master of the node, false if not</returns>
        public static bool IsMaster(this Node Instance)
        {
            return MDStatics.IsNetworkActive() == false || Instance.GetNetworkMaster() == MDStatics.GetPeerId();
        }

        ///<summary>Returns true if we are the client on a network</summary>
        public static bool IsClient(this Node Instance)
        {
            return MDStatics.IsClient();
        }

        ///<summary>Returns the net mode of the game session</summary>
        public static MDNetMode GetNetMode(this Node Instance)
        {
            if (Instance.GetTree().HasNetworkPeer())
            {
                return Instance.GetTree().IsNetworkServer() ? MDNetMode.Server : MDNetMode.Client;
            }

            return MDNetMode.Standalone;
        }

        ///<summary>Removes this node from its parent</summary>
        public static void RemoveFromParent(this Node Instance)
        {
            if (Godot.Object.IsInstanceValid(Instance) && Godot.Object.IsInstanceValid(Instance.GetParent()))
            {
                Instance.GetParent().RemoveChild(Instance);
            }
        }

        ///<summary>Removes this node from its parent and frees it</summary>
        public static void RemoveAndFree(this Node Instance)
        {
            if (Godot.Object.IsInstanceValid(Instance))
            {
                Instance.RemoveFromParent();
                Instance.QueueFree();
            }
        }

        // 
        /// <summary>
        /// Same as Rpc except it checks if the network is activate first and takes game clock 1o account
        /// </summary>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDRpc(this Node Instance, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive())
            {
                return;
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(-1, MDReliability.Reliable, Instance, Method, Args);
        }

        /// <summary>
        /// Same as RpcId except it checks if the network is activate first and takes game clock into account
        /// </summary>
        /// <param name="PeerId">The id of the peer to send to</param>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDRpcId(this Node Instance, int PeerId, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(PeerId, MDReliability.Reliable, Instance, Method, Args);
        }

        /// <summary>
        /// Same as RpcUnreliable except it checks if the network is activate first and takes game clock into account
        /// </summary>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDRpcUnreliable(this Node Instance, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(-1, MDReliability.Unreliable, Instance, Method, Args);
        }

        /// <summary>
        /// Same as RpcUnreliableId except it checks if the network is activate first and takes game clock into account
        /// </summary>
        /// <param name="PeerId">The id of the peer to send to</param>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDRpcUnreliableId(this Node Instance, int PeerId, string Method, params object[] Args)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            // Send through replicator
            MDStatics.GetReplicator().SendClockedRpc(PeerId, MDReliability.Unreliable, Instance, Method, Args);
        }

        /// <summary>
        /// Same as Rset except it checks if the network is activate first
        /// </summary>
        /// <param name="Property">The property to set</param>
        /// <param name="Value">The value</param>
        public static void MDRset(this Node Instance, string Property, object Value)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            MDStatics.GetReplicator().SendClockedRset(-1, MDReliability.Reliable, Instance, Property, Value);
        }

        /// <summary>
        /// Same as RsetId except it checks if the network is activate first
        /// </summary>
        /// <param name="PeerId">The peer to send to</param>
        /// <param name="Property">The property to set</param>
        /// <param name="Value">The value</param>
        public static void MDRsetId(this Node Instance, int PeerId, string Property, object Value)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            MDStatics.GetReplicator().SendClockedRset(PeerId, MDReliability.Reliable, Instance, Property, Value);
        }

        /// <summary>
        /// Same as RsetUnreliable except it checks if the network is activate first
        /// </summary>
        /// <param name="Property">The property to set</param>
        /// <param name="Value">The value</param>
        public static void MDRsetUnreliable(this Node Instance, string Property, object Value)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }
            
            MDStatics.GetReplicator().SendClockedRset(-1, MDReliability.Unreliable, Instance, Property, Value);
        }

        /// <summary>
        /// Same as RsetUnreliable except it checks if the network is activate first
        /// </summary>
        /// <param name="PeerId">The peer to send to</param>
        /// <param name="Property">The property to set</param>
        /// <param name="Value">The value</param>
        public static void MDRsetUnreliableId(this Node Instance, int PeerId, string Property, object Value)
        {
            if (!MDStatics.IsNetworkActive() && !MDStatics.IsServer())
            {
                return;
            }

            MDStatics.GetReplicator().SendClockedRset(PeerId, MDReliability.Unreliable, Instance, Property, Value);
        }

        /// <summary>
        /// Sends the RPC to the server only
        /// </summary>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDServerRpc(this Node Instance, string Method, params object[] Args)
        {
            int ServerId = Instance.GetGameSession().GetNetworkMaster();
            Instance.MDRpcId(ServerId, Method, Args);
        }

        /// <summary>
        /// Sends the unreliable RPC to the server only
        /// </summary>
        /// <param name="Method">The method to call</param>
        /// <param name="Args">Arguments</param>
        public static void MDServerRpcUnreliable(this Node Instance, string Method, params object[] Args)
        {
            int ServerId = Instance.GetGameSession().GetNetworkMaster();
            Instance.MDRpcUnreliableId(ServerId, Method, Args);
        }

        /// <summary>
        /// Invoke the given method on the node
        /// </summary>
        /// <param name="Method">The method to invoke</param>
        /// <param name="Parameters">Parameters</param>
        /// <returns>True if invoked, false if not found</returns>
        public static bool Invoke(this Node Instance, String Method, params object[] Parameters)
        {
            MethodInfo Info = MDStatics.GetMethodInfo(Instance, Method, Parameters);
            if (Info != null)
            {
                MDLog.Trace(LOG_CAT, $"Invoking {Info.Name} with parameters {MDStatics.GetParametersAsString(Parameters)}");
                Info.Invoke(Instance, Parameters);
                return true;
            }

            MDLog.Trace(LOG_CAT, $"Failed to invoke {Method} with parameters {MDStatics.GetParametersAsString(Parameters)}");
            return false;
        }

        /// /// <summary>
        /// Invoke the method with the given number on the node
        /// </summary>
        /// <param name="MethodNumber">The method number to invoke</param>
        /// <param name="Parameters">Parameters</param>
        /// <returns>True if invoked, false if not found</returns>
        public static bool Invoke(this Node Instance, int MethodNumber, params object[] Parameters)
        {
            MethodInfo Info = MDStatics.GetMethodInfo(Instance, MethodNumber);
            if (Info != null)
            {
                MDLog.Trace(LOG_CAT, $"Invoking {Info.Name} with parameters {MDStatics.GetParametersAsString(Parameters)}");
                Info.Invoke(Instance, Parameters);
                return true;
            }

            MDLog.Trace(LOG_CAT, $"Failed to invoke method number {MethodNumber} with parameters {MDStatics.GetParametersAsString(Parameters)}");
            return false;
        }
        

        /// <summary>
        /// Set the value of a member on the node
        /// </summary>
        /// <param name="Name">The name of the member</param>
        /// <param name="Value">The value</param>
        /// <returns>True if set, false if not</returns>
        public static bool SetMemberValue(this Node Instance, String Name, object Value)
        {
            MemberInfo Member = MDStatics.GetMemberInfo(Instance, Name);
            if (Member != null)
            {
                Member.SetValue(Instance, Value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Shortcut for GetTree().GetRpcSenderId()
        /// </summary>
        /// <param name="Instance">The Instance from where this function called</param>
        /// <returns>The RPC sender PeerId</returns>
        public static int GetRpcSenderId(this Node Instance)
        {
            return Instance.GetTree().GetRpcSenderId();
        }

        /// <summary>Creates a timer as a child of the current node</summary>
        /// <param name="Instance">The Instance from where this function called</param>
        /// <param name="Name">The name of the timer</param>
        /// <param name="OneShot">Is this a one shot timer</param>
        /// <param name="WaitTime">Duration of the timer</param>
        /// <param name="TimerAsFirstArgument">Should we pass the timer as the first argument to the timeout method?</param>
        /// <param name="ConnectionTarget">The object to attach the timeout method to</param>
        /// <param name="MethodName">The name of the timeout method</param>
        /// <param name="Parameters">Array of parameters to pass to the timeout function</param>
        /// <returns>The new timer</returns>
        public static Timer CreateTimer(this Node Instance, String Name, bool OneShot, float WaitTime,
            bool TimerAsFirstArgument, Godot.Object ConnectionTarget, String MethodName, params object[] Parameters)
        {
            Timer timer = new Timer
            {
                Name = Name,
                OneShot = OneShot,
                WaitTime = WaitTime
            };
            List<object> parameters = new List<object>();
            if (TimerAsFirstArgument)
            {
                parameters.Add(timer);
            }

            parameters.AddRange(Parameters);

            timer.Connect("timeout", ConnectionTarget, MethodName, new Godot.Collections.Array(parameters));
            Instance.AddChild(timer);
            return timer;
        }

        /// <summary>
        /// Creates a timer that still runs while game is paused
        /// </summary>
        /// <param name="Instance">The Instance from where this function called</param>
        /// <param name="Name">The name of the timer</param>
        /// <param name="OneShot">Is this a one shot timer</param>
        /// <param name="WaitTime">Duration of the timer</param>
        /// <param name="TimerAsFirstArgument">Should we pass the timer as the first argument to the timeout method?</param>
        /// <param name="ConnectionTarget">The object to attach the timeout method to</param>
        /// <param name="MethodName">The name of the timeout method</param>
        /// <param name="Parameters">Array of parameters to pass to the timeout function</param>
        /// <returns>The new timer</returns>
        public static Timer CreateUnpausableTimer(this Node Instance, string TimerName, bool OneShot, float WaitTime, bool TimerAsFirstArgument,
            Godot.Object ConnectionTarget, string MethodName, params object[] Parameters)
        {
            Timer timer = Instance.CreateTimer(TimerName, OneShot, WaitTime, 
                                                TimerAsFirstArgument, ConnectionTarget, MethodName, Parameters);
            timer.PauseMode = Node.PauseModeEnum.Process;
            return timer;
        }

        /// <summary>
        /// Get the sender ID of the current MDRpc call
        /// </summary>
        /// <returns>The sender ID or -1 if not inside an MDRpc call</returns>
        public static int MDGetRpcSenderId(this Node Instance)
        {
            return MDStatics.GetReplicator().RpcSenderId;
        }

        /// <summary>
        /// Changes the network master of the node, this only works on the server
        /// </summary>
        /// <param name="NewNetworkMaster">The new network master ID</param>
        public static void ChangeNetworkMaster(this Node Instance, int NewNetworkMaster)
        {
            MDGameSession GameSession = Instance.GetGameSession();
            GameSession.ChangeNetworkMaster(Instance, NewNetworkMaster);

        }

        public static Node MDFindNode(this Node Instance, string PathToNode)
        {
            // First, if we have an explicit node path, try that
            Node BoundNode = Instance.GetNodeOrNull(PathToNode);
            if (BoundNode != null)
            {
                return BoundNode;
            }

            // Check if we have a child with the same name
            Godot.Collections.Array Children = Instance.GetChildren();
            foreach (Node Child in Children)
            {
                if (Child != null && Child.Name == PathToNode)
                {
                    return Child;
                }
            }

            // Then check if a child has the node instead
            foreach (Node Child in Children)
            {
                BoundNode = MDFindNode(Child, PathToNode);
                if (BoundNode != null)
                {
                    return BoundNode;
                }
            }

            return null;
        }
    }
}