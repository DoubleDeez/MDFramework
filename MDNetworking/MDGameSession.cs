using Godot;
using System;
using PlayerListType = System.Collections.Generic.Dictionary<int, MDPlayerInfo>;

/*
 * MDGameSession
 *
 * Class that manages the current multiplayer state of the game.
 */
public class MDGameSession : Node
{
    private const string DEFAULT_IP = "127.0.0.1";
    private const int DEFAULT_PORT = 7777;
    private const int DEFAULT_MAX_PLAYERS = 32;
    private const string ARG_SERVER = "server";
    private const string ARG_CLIENT = "client";
    private const string LOG_CAT = "LogGameSession";

    public const int STANDALONE_ID = 0;
    public const int SERVER_ID = 1;
    public const string PlayerNameFormat = "Player{0}";

    public override void _Ready()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Debug));
        this.RegisterCommandAttributes();

        CheckArgsForConnectionInfo();
    }

    [MDCommand]
    public bool StartStandalone()
    {
        MDLog.Info(LOG_CAT, "Starting Standalone Game Session");
        StandaloneOnStarted();
        return true;
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_PORT, DEFAULT_MAX_PLAYERS})]
    public bool StartServer(int Port, int MaxPlayers = DEFAULT_MAX_PLAYERS)
    {
        NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
        peer.Connect("peer_connected", this, nameof(ServerOnPeerConnected));
        peer.Connect("peer_disconnected", this, nameof(ServerOnPeerDisconnected));

        Error error = peer.CreateServer(Port, MaxPlayers);
        bool Success = error == Error.Ok;
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Starting server on port {0} with {1} max players.", Port, MaxPlayers);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to start server on port {0}", Port);

        if (Success)
        {
            GetTree().NetworkPeer = peer;
            ServerOnStarted();
        }
        else
        {
            ServerOnFailedToStart();
        }

        return Success;
    }

    [MDCommand(DefaultArgs = new object[] {DEFAULT_IP, DEFAULT_PORT})]
    public bool StartClient(string Address, int Port)
    {
        NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
        peer.Connect("connection_succeeded", this, nameof(ClientOnConnected));
        peer.Connect("connection_failed", this, nameof(ClientOnFailedToConnect));
        peer.Connect("server_disconnected", this, nameof(ClientOnServerDisconnect));
        
        Error error = peer.CreateClient(Address, Port);
        bool Success = error == Error.Ok;
        MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, "Connecting to server at {0}:{1}", Address, Port);
        MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, "Failed to connect to server at {0}:{1}", Address, Port);

        if (Success)
        {
            GetTree().NetworkPeer = peer;
        }

        return Success;
    }

    [MDCommand()]
    public void Disconnect()
    {
        using (NetworkedMultiplayerENet peer = GetPeer())
        {
            OnDisconnect();
            if (peer != null)
            {
                peer.CloseConnection();
                GetTree().NetworkPeer = null;
            }
        }
    }

    public NetworkedMultiplayerENet GetPeer()
    {
        NetworkedMultiplayerPeer peer = GetTree().NetworkPeer;
        if (peer is NetworkedMultiplayerENet)
        {
            return (NetworkedMultiplayerENet)peer;
        }

        return null;
    }

    protected virtual void StandaloneOnStarted()
    {
        GetOrCreatePlayerObject(STANDALONE_ID);
        OnPlayerJoined(STANDALONE_ID);
    }

    protected virtual void ServerOnStarted()
    {
        MDLog.Info(LOG_CAT, "Server started");
        // TODO - Dedicated server support
        GetOrCreatePlayerObject(SERVER_ID);
        OnPlayerJoined(SERVER_ID);
    }
    protected virtual void ServerOnFailedToStart()
    {
        MDLog.Error(LOG_CAT, "Failed to start server");
    }

    protected virtual void ClientOnConnected()
    {
        MDLog.Info(LOG_CAT, "Client connected to server");
        int PeerId = MDStatics.GetPeerID();
        GetOrCreatePlayerObject(PeerId);
        OnPlayerJoined(PeerId);
    }

    protected virtual void ClientOnFailedToConnect()
    {
        MDLog.Error(LOG_CAT, "Client failed to connect to server");
    }

    protected void ClientOnServerDisconnect()
    {
        MDLog.Error(LOG_CAT, "Client was disconnected from server");
        ClientOnDisconnectedFromServer();
    }

    // Called on the server when a client connects
    private void ServerOnPeerConnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
        GetOrCreatePlayerObject(PeerId);
        OnPlayerJoined(PeerId);
        BroadcastNewPlayerJoined(PeerId);
        SendConnectionDataToClient(PeerId);
    }

    // Called on the server when a client disconnects
    private void ServerOnPeerDisconnected(int PeerId)
    {
        MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerId);
        OnPlayerLeft(PeerId);
        BroadcastPlayerLeft(PeerId);
    }

    protected virtual void ClientOnDisconnectedFromServer()
    {
        MDLog.Info(LOG_CAT, "Disconnected from server");
        OnDisconnect();
    }

    // Called on the server when a client first connects
    protected virtual void SendConnectionDataToClient(int Joiner)
    {
        foreach (int PeerId in Players.Keys)
        {
            if (PeerId != Joiner)
            {
                MDLog.Debug(LOG_CAT, "Notifying Peer [{0}] that Peer [{1}] exists", Joiner, PeerId);
                RpcId(Joiner, nameof(ClientOnPlayerJoined), PeerId);
                Players[PeerId].PerformFullSync(Joiner);
            }
        }
    }

    // Notifies all clients (except the new one) that a new player has joined
    protected virtual void BroadcastNewPlayerJoined(int Joiner)
    {
        foreach (int PeerId in Players.Keys)
        {
            if (PeerId != Joiner && PeerId != SERVER_ID)
            {
                MDLog.Debug(LOG_CAT, "Notifying Peer [{0}] that Peer [{1}] joined", PeerId, Joiner);
                RpcId(PeerId, nameof(ClientOnPlayerJoined), Joiner);
            }
        }
    }

    // Notifies all clients that a player has left
    protected virtual void BroadcastPlayerLeft(int Leaver)
    {
        foreach (int PeerId in Players.Keys)
        {
            if (PeerId != Leaver && PeerId != SERVER_ID)
            {
                MDLog.Debug(LOG_CAT, "Notifying Peer [{0}] that Peer [{1}] left", PeerId, Leaver);
                RpcId(PeerId, nameof(ClientOnPlayerLeft), Leaver);
            }
        }
    }

    // Called on a client, notifying them that a player joined
    [Puppet]
    private void ClientOnPlayerJoined(int PeerId)
    {
        GetOrCreatePlayerObject(PeerId);
        OnPlayerJoined(PeerId);
    }

    // Called whenever a player joined, includes self and existing players when initially joining
    protected virtual void OnPlayerJoined(int PeerId)
    {
    }

    // Called on a client, notifying them that a player left
    [Puppet]
    private void ClientOnPlayerLeft(int PeerId)
    {
        OnPlayerLeft(PeerId);
        RemovePlayerObject(PeerId);
    }

    // Called whenever a player left
    protected virtual void OnPlayerLeft(int PeerId)
    {
    }

    // Called when disconnected from the server or as the server
    protected virtual void OnDisconnect()
    {
        foreach (int PeerId in Players.Keys)
        {
            MDPlayerInfo Player = Players[PeerId];
            Player.RemoveAndFree();
        }

        Players.Clear();
    }

    // Create and initialize the player object
    protected virtual MDPlayerInfo GetOrCreatePlayerObject(int PeerId)
    {
        if (Players.ContainsKey(PeerId))
        {
            return Players[PeerId];
        }

        Type PlayerType = GetPlayerInfoType();
        if (!MDStatics.IsSameOrSubclass(PlayerType, typeof(MDPlayerInfo)))
        {
            MDLog.Error(LOG_CAT, "Provided player type [{0}] is not a subclass of MDPlayerInfo", PlayerType.Name);
            return null;
        }

        string PlayerName = String.Format(PlayerNameFormat, PeerId);
        MDPlayerInfo Player = Activator.CreateInstance(PlayerType) as MDPlayerInfo;
        Player.InitPlayerInfo(PeerId);
        AddChild(Player);

        Players.Add(PeerId, Player);
        return Player;
    }

    // Removes the MDPlayerInfo belonging to the PeerId
    protected virtual void RemovePlayerObject(int PeerId)
    {
        if (Players.ContainsKey(PeerId))
        {
            MDPlayerInfo Player = Players[PeerId];
            Player.RemoveAndFree();
            Players.Remove(PeerId);
        }
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
    protected virtual Type GetPlayerInfoType()
    {
        return typeof(MDPlayerInfo);
    }

    protected PlayerListType Players = new PlayerListType();
}
