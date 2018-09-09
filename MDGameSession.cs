using Godot;
using System;
using System.Text;
using System.Reflection;

public static class MDPacketType
{
    public const int Replication = 0;

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

        SetProcess(false);
    }

    public override void _Process(float delta)
    {
        Replicator.TickReplication(delta);
    }

    // Called whenever we recieve an network event (only during _process and there could be multiple)
    private void OnNetEvent(GDNetEvent Event)
    {
        GDNetEvent.Type EventType = Event.GetEventType();
        if (EventType == GDNetEvent.Type.Connect)
        {
            if (NetEntity.GetNetMode() == MDNetMode.Server)
            {
                ServerOnPeerConnected(Event.GetPeerId());
            }
            else
            {
                ClientOnConnectedToServer();
            }
        }
        else if (EventType == GDNetEvent.Type.Disconnect)
        {
            if (NetEntity.GetNetMode() == MDNetMode.Server)
            {
                ServerOnPeerDisconnected(Event.GetPeerId());
            }
            else
            {
                ClientOnConnectedToServer();
            }
        }
        else if (EventType == GDNetEvent.Type.Receive)
        {
            OnDataReceived(Event);
        }
        else
        {
            MDLog.Error(LOG_CAT, "Received unhandled net event of type ({0})", EventType.ToString());
        }
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_PORT})]
    public bool StartServer(int Port)
    {
        bool Success = NetEntity.StartServer(Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Started server on port {0}", Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to start server on port {0}", Port);

        if (Success)
        {
            SetProcess(true);
        }

        return Success;
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_IP, DEFAULT_PORT})]
    public bool StartClient(string Address, int Port)
    {
        bool Success = NetEntity.ConnectToServer(Address, Port);
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Connected to server at {0}:{1}", Address, Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to connect to server at {0}:{1}", Address, Port);

        if (Success)
        {
            SetProcess(true);
        }

        return Success;
    }

    [MDCommand()]
    public void Disconnect()
    {
        NetEntity.Disconnect();
    }

    [MDCommand()]
    public void SendRawData(string Data)
    {
        NetEntity.SendBytes(Encoding.UTF8.GetBytes(Data));
    }

    protected void ServerOnPeerConnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
    }

    protected void ClientOnConnectedToServer()
    {
        MDLog.Info(LOG_CAT, "Connected to server");
    }

    protected void ServerOnPeerDisconnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerId);
    }

    protected void ClientOnDisconnectedFromServer()
    {
        MDLog.Info(LOG_CAT, "Disconnected from server");
    }

    protected void OnDataReceived(GDNetEvent Event)
    {
        MDLog.Info(LOG_CAT, "Received data from peer [ID: {0}]:", Event.GetPeerId());
        MDLog.Info(LOG_CAT, "Data: [{0}]", Event.GetData());
        if (Event.GetPacket() != null)
        {
            MDLog.Info(LOG_CAT, "Packet of length [{0}], data: [{1}]", Event.GetPacket().Length, Encoding.UTF8.GetString(Event.GetPacket()));
        }
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

    public MDNetEntity NetEntity { get; private set;}
    public MDReplicator Replicator {get; private set;}
}
