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
    public const int PlayerJoined = 4;
    public const int PlayerLeft = 5;

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
        this.RegisterCommandAttributes();

        NetEntity.OnNetEvent = OnNetEvent;

        CheckArgsForConnectionInfo();

        GetTree().Connect("idle_frame", this, nameof(PreTick));
    }

    private void PreTick()
    {
        NetEntity.ReadNetEvents();

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
            OnServerStarted();
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

    protected virtual void OnServerStarted()
    {
        MDLog.Info(LOG_CAT, "Server started");
    }

    protected virtual void ServerOnPeerConnected(int PeerID)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerID);
        SendConnectionDataToClient(PeerID);
        BroadcastNewPlayerJoined(PeerID);

        MDLog.Debug(LOG_CAT, "Peers: {0}", Peers.Keys.ToString());

        // TODO - Call this in a spot that guarantees the client is ready for it
        Replicator.BuildAllNodeDataAndSendToPeer(PeerID);
    }

    protected virtual void ClientOnConnectedToServer()
    {
        MDLog.Info(LOG_CAT, "Connected to server");
    }

    protected virtual void ServerOnPeerDisconnected(int PeerID)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerID);
        RemovePlayerObject(PeerID);
        BroadcastPlayerLeft(PeerID);

        MDLog.Debug(LOG_CAT, "Peers: {0}", Peers.Keys.ToString());
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
                case MDPacketType.PlayerJoined:
                    ClientOnPlayerJoined(PacketNoType);
                    break;
                case MDPacketType.PlayerLeft:
                    ClientOnPlayerLeft(PacketNoType);
                    break;
                default:
                    MDLog.Error(LOG_CAT, "Received unknown packet type [{0}]", PacketType);
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
        RemoteCaller.SetNetworkOwner(Player.GetName(), PeerID);
    }

    // Notifies all clients (except the new one) that a new player has joined
    protected virtual void BroadcastNewPlayerJoined(int Joiner)
    {
        foreach (int Peer in Peers.Keys)
        {
            if (Peer != Joiner)
            {
                MDLog.Debug(LOG_CAT, "Notifying Peer [{0}] that Peer [{1}] joined", Peer, Joiner);
                SendPacket(Peer, MDPacketType.PlayerJoined, MDSerialization.ConvertSupportedTypeToBytes(Joiner));
            }
        }
    }

    // Notifies all clients that a player has left
    protected virtual void BroadcastPlayerLeft(int Leaver)
    {
        foreach (int Peer in Peers.Keys)
        {
            if (Peer != Leaver)
            {
                MDLog.Debug(LOG_CAT, "Notifying Peer [{0}] that Peer [{1}] left", Peer, Leaver);
                SendPacket(Peer, MDPacketType.PlayerLeft, MDSerialization.ConvertSupportedTypeToBytes(Leaver));
            }
        }
    }

    // Called on a client, notifying them that a player joined
    protected virtual void ClientOnPlayerJoined(byte[] Data)
    {
        if (this.GetNetMode() != MDNetMode.Client)
        {
            MDLog.Error(LOG_CAT, "Received PlayerJoined packet but we are not a client");
            return;
        }

        int Joiner = MDSerialization.GetIntFromStartOfByteArray(Data);
        MDLog.Info(LOG_CAT, "Player [ID: {0}] joined", Joiner);
        CreatePlayerObject(Joiner);
    }

    // Called on a client, notifying them that a player left
    protected virtual void ClientOnPlayerLeft(byte[] Data)
    {
        if (this.GetNetMode() != MDNetMode.Client)
        {
            MDLog.Error(LOG_CAT, "Received PlayerLeft packet but we are not a client");
            return;
        }

        int Leaver = MDSerialization.GetIntFromStartOfByteArray(Data);
        MDLog.Info(LOG_CAT, "Player [ID: {0}] left", Leaver);
        RemovePlayerObject(Leaver);
    }

    // After the client connects to the server, the server will send the client data to this function
    protected virtual void OnReceivedConnectionData(byte[] Data)
    {
        if(this.GetNetMode() != MDNetMode.Server)
        {
            LocalPeerID = MDSerialization.ConvertBytesToSupportedType<int>(Data);
            CreatePlayerObject(LocalPeerID);
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
        if (Peers.ContainsKey(PeerID))
        {
            return Peers[PeerID];
        }

        Type PlayerType = GetPlayerType();
        if (!MDStatics.IsSameOrSubclass(PlayerType, typeof(MDPlayer)))
        {
            MDLog.Error(LOG_CAT, "Provided player type [{0}] is not a subclass of MDPlayer", PlayerType.Name);
            return null;
        }

        string PlayerName = String.Format(PlayerNameFormat, PeerID);
        MDPlayer Player = Activator.CreateInstance(PlayerType) as MDPlayer;
        Player.SetName(PlayerName);
        Player.PlayerName = PlayerName;
        Player.PeerID = PeerID;
        this.AddNodeToRoot(Player);

        Peers.Add(PeerID, Player);
        return Player;
    }

    // Removes the MDPlayer belonging to the PeerID
    protected virtual void RemovePlayerObject(int PeerID)
    {
        if (Peers.ContainsKey(PeerID))
        {
            MDPlayer Player = Peers[PeerID];
            Player.RemoveAndFree();
            Peers.Remove(PeerID);
        }
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

    // Override this to provide your own Player class type
    protected virtual Type GetPlayerType()
    {
        return typeof(MDPlayer);
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

    private PeerDict Peers = new PeerDict();

    public MDNetEntity NetEntity {get; private set;} = new MDNetEntity();
    public MDReplicator Replicator {get; private set;} = new MDReplicator();
    public MDRemoteCaller RemoteCaller {get; private set;} = new MDRemoteCaller();
    public int LocalPeerID {get; private set; } = STANDALONE_PEER_ID;
}
