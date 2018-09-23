using Godot;
using System;
using System.Reflection;
using PeerDict = System.Collections.Generic.Dictionary<int, MDPlayer>;

public static class MDPacketType
{
    public const int None = 0;
    public const int Replication = 1;
    public const int Connection = 2;
    public const int RPC = 3;

    // Start your own packet types from this value
    public const int MDMaxPacketType = 255;
}

public enum TestEnum
{
    First,
    Second,
    Third
}

/*
 * MDGameSession
 *
 * Class that manages the current multiplayer state of the game.
 */
public class MDGameSession : Node
{
    private const string DEFAULT_IP = "127.0.0.1";
    private const string DEFAULT_PORT = "7777";
    private const string ARG_SERVER = "server";
    private const string ARG_CLIENT = "client";
    private const string LOG_CAT = "LogGameSession";

    public const int STANDALONE_PEER_ID = -2;
    public const int SERVER_PEER_ID = -1;

    public const string PlayerNameFormat = "Player{0}";

    public override void _Ready()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        SetupNetEntity();
        CheckArgsForConnectionInfo();
        this.RegisterCommandAttributes();

        GetTree().Connect("idle_frame", this, nameof(PreProcess));

        SetProcess(true);
    }

    public override void _Process(float delta)
    {
        WorldTime += delta;

        if (WorldTime > 3)
        {
            WorldTime = 0.0f;
            if (this.GetNetMode() == MDNetMode.Server)
            {
                if (TestEnumVal == TestEnum.First)
                {
                    TestEnumVal = TestEnum.Second;
                }
                else if (TestEnumVal == TestEnum.Second)
                {
                    TestEnumVal = TestEnum.Third;
                }
                else if (TestEnumVal == TestEnum.Third)
                {
                    TestEnumVal = TestEnum.First;
                }

                MDLog.Info(LOG_CAT, "Setting TestEnumVal to [{0}]", TestEnumVal);

                this.CallRPC(nameof(BroadcastTestRPC), TestEnumVal);
            }
            else
            {
                MDLog.Info(LOG_CAT, "TestEnumVal is now [{0}]", TestEnumVal);
            }
        }
    }

    private void PreProcess()
    {
        if (this.GetNetMode() == MDNetMode.Server)
        {
            Replicator.TickReplication();
        }
    }

    // Called whenever we recieve an network event (only during _process and there could be multiple)
    private void OnNetEvent(GDNetEvent Event)
    {
        GDNetEvent.Type EventType = Event.GetEventType();
        if (EventType == GDNetEvent.Type.Connect)
        {
           OnConnectedEvent(Event.GetPeerId());
        }
        else if (EventType == GDNetEvent.Type.Disconnect)
        {
            OnDisconnectEvent(Event.GetPeerId());
        }
        else if (EventType == GDNetEvent.Type.Receive)
        {
            OnDataReceived(Event);
        }
        else
        {
            MDLog.Error(LOG_CAT, "Received unhandled net event type ({0})", EventType.ToString());
        }
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_PORT})]
    public bool StartServer(int Port)
    {
        bool Success = NetEntity.StartServer(Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Debug, "Started server on port {0}", Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to start server on port {0}", Port);

        if (Success)
        {
            LocalPeerID = SERVER_PEER_ID;
        }

        return Success;
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_IP, DEFAULT_PORT})]
    public bool StartClient(string Address, int Port)
    {
        bool Success = NetEntity.ConnectToServer(Address, Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Debug, "Connected to server at {0}:{1}", Address, Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to connect to server at {0}:{1}", Address, Port);

        return Success;
    }

    [MDCommand()]
    public void Disconnect()
    {
        LocalPeerID = STANDALONE_PEER_ID;
        NetEntity.Disconnect();
    }

    public void BroadcastPacket(int PacketType, byte[] data, RPCReliability Reliability = RPCReliability.Reliable)
    {
        NetEntity.SendBytes(MDStatics.JoinByteArrays(MDSerialization.ConvertSupportedTypeToBytes(PacketType), data), ConvertReliabilityType(Reliability));
    }

    public void SendPacket(int PacketType, byte[] data, RPCReliability Reliability = RPCReliability.Reliable)
    {
        NetEntity.SendBytes(MDStatics.JoinByteArrays(MDSerialization.ConvertSupportedTypeToBytes(PacketType), data), ConvertReliabilityType(Reliability));
    }

    public void SendPacket(int Peer, int PacketType, byte[] data, RPCReliability Reliability = RPCReliability.Reliable)
    {
        NetEntity.SendBytes(Peer, MDStatics.JoinByteArrays(MDSerialization.ConvertSupportedTypeToBytes(PacketType), data), ConvertReliabilityType(Reliability));
    }

    private void OnConnectedEvent(int PeerID)
    {
        if (this.GetNetMode() == MDNetMode.Server)
        {
            ServerOnPeerConnected(PeerID);
        }
        else
        {
            ClientOnConnectedToServer();
        }
    }

    private void OnDisconnectEvent(int PeerID)
    {
        if (this.GetNetMode() == MDNetMode.Server)
        {
            ServerOnPeerDisconnected(PeerID);
        }
        else
        {
            PeerID = STANDALONE_PEER_ID;
            ClientOnDisconnectedFromServer();
        }
    }

    protected virtual void ServerOnPeerConnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
        SendConnectionDataToClient(PeerId);

        // TODO - Call this in a spot that guarantees the client is ready for it
        Replicator.BuildAllNodeDataAndSendToPeer(PeerId);
    }

    protected virtual void ClientOnConnectedToServer()
    {
        MDLog.Info(LOG_CAT, "Connected to server");
    }

    protected virtual void ServerOnPeerDisconnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerId);
        Peers.Remove(PeerId);
    }

    protected virtual void ClientOnDisconnectedFromServer()
    {
        MDLog.Info(LOG_CAT, "Disconnected from server");
    }

    // Called when we receive a network packet
    protected virtual void OnDataReceived(GDNetEvent Event)
    {
        if (Event.GetPacket() != null)
        {
            byte[] Packet = Event.GetPacket();
            byte[] PacketNoType = Packet.SubArray(4);
            int PacketType = GetPacketTypeFromBytes(Packet);
            MDLog.Debug(LOG_CAT, "Received data from peer [ID: {0}] of Packet Type [{1}]", Event.GetPeerId(), PacketType);
            MDLog.Debug(LOG_CAT, "Packet of length [{0}], data: [{1}]", PacketNoType.Length, MDSerialization.ConvertBytesToSupportedType(MDSerialization.Type_String, PacketNoType));
            
            switch(PacketType)
            {
                case MDPacketType.None:
                    break;
                case MDPacketType.Replication:
                    OnReceivedReplicationData(PacketNoType);
                    break;
                case MDPacketType.Connection:
                    OnReceivedConnectionData(PacketNoType);
                    break;
                case MDPacketType.RPC:
                    OnReceivedRpcData(PacketNoType, Event.GetPeerId());
                    break;
            }
        }
    }

    // Called on the server when a client first connects
    protected virtual void SendConnectionDataToClient(int PeerID)
    {
        byte[] PeerIDAsBytes = MDSerialization.ConvertSupportedTypeToBytes(PeerID);
        SendPacket(PeerID, MDPacketType.Connection, PeerIDAsBytes);

        MDPlayer Player = CreatePlayerObject(PeerID);
        Peers.Add(PeerID, Player);

        RemoteCaller.SetNetworkOwner(Player.GetName(), PeerID);
    }

    // After the client connects to the server, the server will send the client data to this function
    protected virtual void OnReceivedConnectionData(byte[] Data)
    {
        if(this.GetNetMode() != MDNetMode.Server)
        {
            LocalPeerID = MDSerialization.ConvertBytesToSupportedType<int>(Data);
            Peers.Add(LocalPeerID, CreatePlayerObject(LocalPeerID));
        }
        else
        {
            MDLog.Error(LOG_CAT, "Received connection packet but we are the server");
        }
    }

    // When the client receives replication data from the server, this function is called
    protected virtual void OnReceivedReplicationData(byte[] Data)
    {
        if(this.GetNetMode() != MDNetMode.Server)
        {
            Replicator.UpdateChanges(Data);
        }
        else
        {
            MDLog.Error(LOG_CAT, "Received replication packet but we are the server");
        }
    }

    // We received an RPC call
    protected virtual void OnReceivedRpcData(byte[] Data, int SenderID)
    {
        RemoteCaller.HandleRPCPacket(Data, SenderID);
    }

    // Create and initialize the player object
    protected virtual MDPlayer CreatePlayerObject(int PeerID)
    {
        MDPlayer Player = new MDPlayer();
        string PlayerName = String.Format(PlayerNameFormat, PeerID);
        Player.SetName(PlayerName);
        this.AddNodeToRoot(Player);

        return Player;
    }

    // Gets the MDPacketType from the beginning of a byte array
    private int GetPacketTypeFromBytes(byte[] bytes)
    {
        return MDSerialization.GetIntFromStartOfByteArray(bytes);
    }

    // Starts a server or client based on the command args
    private void CheckArgsForConnectionInfo()
    {
        // Expects -server=[port]
        if (MDArguments.HasArg(ARG_SERVER))
        {
            int Port = MDArguments.GetArgInt(ARG_SERVER);
            StartServer(Port);
        }
        // Expects -client=[IPAddres:Port]
        else if (MDArguments.HasArg(ARG_CLIENT))
        {
            string ClientArg = MDArguments.GetArg(ARG_CLIENT);
            string[] HostPort = ClientArg.Split(":");
            if (HostPort.Length == 2)
            {
                StartClient(HostPort[0], HostPort[1].ToInt());
            }
            else
            {
                MDLog.Error(LOG_CAT, "Failed to parse client arg {0}, expecting -{1}=[IPAddres:Port]", ClientArg, ARG_CLIENT);
            }
        }
    }

    // Ensure NetEntity is created
    private void SetupNetEntity()
    {
        if (NetEntity == null)
        {
            NetEntity = new MDNetEntity();
            NetEntity.OnNetEvent = OnNetEvent;
            this.AddNodeToRoot(NetEntity);
        }
    }

    // Register the passed in node's rpc methods
    public void RegisterRPCs(Node Instance)
    {
        RemoteCaller.RegisterRPCs(Instance);
    }

    // Unregister the passed in node's rpc methods
    public void UnregisterRPCs(Node Instance)
    {
        RemoteCaller.UnregisterRPCs(Instance);
    }

    // Call an RPC function
    public void CallRPC(Node Instance, string FunctionName, params object[] args)
    {
        RemoteCaller.CallRPC(Instance, FunctionName, args);
    }

    // Convert RPC reliability enum to GDNet's type
    private GDNetMessage.Type ConvertReliabilityType(RPCReliability Reliability)
    {
        switch (Reliability)
        {
            case RPCReliability.Reliable:
                return GDNetMessage.Type.Reliable;
            case RPCReliability.Unreliable:
                return GDNetMessage.Type.Sequenced;
            case RPCReliability.Unordered:
                return GDNetMessage.Type.Unsequenced;
            default:
                MDLog.Error(LOG_CAT, "Invalid RPCReliability type [{0}] for conversion", Reliability);
                return 0;
        }
    }

    [MDRpc(RPCType.Broadcast, RPCReliability.Reliable)]
    private void BroadcastTestRPC(TestEnum Val)
    {
        MDLog.Info(LOG_CAT, "Test Broadcast RPC Value [{0}]", Val);
    }

    [MDReplicated()]
    private TestEnum TestEnumVal = TestEnum.First;

    private float WorldTime = 0.0f;

    private PeerDict Peers = new PeerDict();

    [MDBindNode("/root")]
    private Node RootField;

    public MDNetEntity NetEntity {get; private set;}
    public MDReplicator Replicator {get; private set;} = new MDReplicator();
    public MDRemoteCaller RemoteCaller {get; private set;} = new MDRemoteCaller();
    public int LocalPeerID {get; private set; } = STANDALONE_PEER_ID;
}
