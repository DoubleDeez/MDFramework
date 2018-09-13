using Godot;
using System;
using System.Reflection;

public static class MDPacketType
{
    public const int None = 0;
    public const int Replication = 1;
    public const int Connection = 2;

    // Start your own packet types from this value
    public const int MDMaxPacketType = 255;
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

    public override void _Ready()
    {
        CreateReplicator();
        SetupNetEntity();
        CheckArgsForConnectionInfo();
        this.RegisterCommandAttributes();

        GetTree().Connect("idle_frame", this, "PreProcess");

        this.RegisterReplicatedFields();

        SetProcess(true);
    }

    public override void _Process(float delta)
    {
        WorldTime += delta;

        if (WorldTime > 3)
        {
            WorldTime = 0.0f;

            TestInt = Rand.Next(33, 127);
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
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Started server on port {0}", Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to start server on port {0}", Port);

        return Success;
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_IP, DEFAULT_PORT})]
    public bool StartClient(string Address, int Port)
    {
        bool Success = NetEntity.ConnectToServer(Address, Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Connected to server at {0}:{1}", Address, Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to connect to server at {0}:{1}", Address, Port);

        return Success;
    }

    [MDCommand()]
    public void Disconnect()
    {
        NetEntity.Disconnect();
        Engine.SetTargetFps(0);
    }

    [MDCommand()]
    public void SendString(string Data)
    {
        SendPacket(MDPacketType.None, MDSerialization.ConvertSupportedTypeToBytes(Data));
    }

    public void SendPacket(int PacketType, byte[] data)
    {
        NetEntity.SendBytes(MDStatics.JoinByteArrays(MDSerialization.ConvertSupportedTypeToBytes(PacketType), data));
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
            ClientOnDisconnectedFromServer();
        }
    }

    protected virtual void ServerOnPeerConnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
    }

    protected virtual void ClientOnConnectedToServer()
    {
        MDLog.Info(LOG_CAT, "Connected to server");
    }

    protected virtual void ServerOnPeerDisconnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerId);
    }

    protected virtual void ClientOnDisconnectedFromServer()
    {
        MDLog.Info(LOG_CAT, "Disconnected from server");
    }

    protected virtual void OnDataReceived(GDNetEvent Event)
    {
        if (Event.GetPacket() != null)
        {
            byte[] Packet = Event.GetPacket();
            byte[] PacketNoType = Packet.SubArray(4);
            int PacketType = GetPacketTypeFromBytes(Packet);
            MDLog.Info(LOG_CAT, "Received data from peer [ID: {0}] of Packet Type [{1}]", Event.GetPeerId(), PacketType);
            
            switch(PacketType)
            {
                case MDPacketType.None:
                    MDLog.Info(LOG_CAT, "Packet of length [{0}], data: [{1}]", PacketNoType.Length, MDSerialization.ConvertBytesToSupportedType(MDSerialization.TypeString, PacketNoType));
                    break;
                case MDPacketType.Replication:
                    if(this.GetNetMode() != MDNetMode.Server)
                    {
                        MDLog.Info(LOG_CAT, "Packet of length [{0}], data: [{1}]", PacketNoType.Length, MDSerialization.ConvertBytesToSupportedType(MDSerialization.TypeString, PacketNoType));
                        Replicator.UpdateChanges(PacketNoType);
                    }
                    else
                    {
                        MDLog.Error(LOG_CAT, "Received replication packet but we are the server");
                    }
                    break;
                case MDPacketType.Connection:
                    // Do something with the connection info
                    break;
            }
        }
    }

    private int GetPacketTypeFromBytes(byte[] bytes)
    {
        return MDSerialization.GetIntFromStartOfByteArray(bytes);
    }

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

    // Ensure Replicator is created
    private void CreateReplicator()
    {
        if (Replicator == null)
        {
            Replicator = new MDReplicator();
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

    [MDReplicated()]
    private int TestInt;

    private float WorldTime = 0.0f;

    private Random Rand = new Random();

    public MDNetEntity NetEntity { get; private set;}
    public MDReplicator Replicator {get; private set;}
}
