using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// Class that manages the current multiplayer state of the game.
    /// </summary>
    [MDAutoRegister]
    public class MDGameSession : Node
    {
        private const string DEFAULT_IP = "127.0.0.1";
        private const int DEFAULT_PORT = 7777;
        private const int DEFAULT_MAX_PLAYERS = 32;
        private const string ARG_STANDALONE = "standalone";
        private const string ARG_SERVER = "server";
        private const string ARG_CLIENT = "client";
        private const string LOG_CAT = "LogGameSession";

        public const int STANDALONE_ID = 0;
        public const int SERVER_ID = 1;
        public const string PlayerNameFormat = "Player{0}";

        public MDGameInstance GameInstance = null;

        public bool IsSessionStarted { get; protected set; } = false;
        public int PlayersCount => Players.Count;

        public string ExternalAddress { get; protected set; } = "";

        /// <summary>
        /// Event handler for events related to a specific player
        /// </summary>
        /// <param name="PeerId">The peerId of the affected player</param>
        public delegate void PlayerEventHandler(int PeerId);

        /// <summary>
        /// Triggered on all clients whenever a player joins before initializing the player, including for the local player and existing players when joining in progress
        /// </summary>
        public event PlayerEventHandler OnPlayerJoinedEvent = delegate { };
        /// <summary>
        /// Triggered on all clients when a player has completed initialization
        /// </summary>
        public event PlayerEventHandler OnPlayerInitializedEvent = delegate { };
        /// <summary>
        /// Triggered on all clients when a player leaves, right before the PlayerInfo is destroyed, also fires locally for each player when disconnecting from the server
        /// </summary>
        public event PlayerEventHandler OnPlayerLeftEvent = delegate { };

        /// <summary>
        /// Event handler for game session events
        /// </summary>
        public delegate void SessionEventHandler();

        /// <summary>
        /// Triggered when the session begins for any mode (standalone, server, client)
        /// </summary>
        public event SessionEventHandler OnSessionStartedEvent = delegate { };
        /// <summary>
        /// Triggered when the session cannot start for server or client
        /// </summary>
        public event SessionEventHandler OnSessionFailedEvent = delegate { };
        /// <summary>
        /// Triggered when the session ends (disconnects)
        /// </summary>
        public event SessionEventHandler OnSessionEndedEvent = delegate { };

        /// <summary>
        /// Event handler for networked node events
        /// </summary>
        /// <param name="node">The affected networked node</param>
        public delegate void NetworkNodeEventHandler(Node node);

        /// <summary>
        /// Triggered on all clients when a networked node is spawned
        /// </summary>
        public event NetworkNodeEventHandler OnNetworkNodeAdded = delegate { };
        /// <summary>
        /// Triggered on all clients right before the node is removed
        /// </summary>
        public event NetworkNodeEventHandler OnNetworkNodeRemoved = delegate { };

        public MDReplicator Replicator { get; set; }
        protected Dictionary<int, MDPlayerInfo> Players = new Dictionary<int, MDPlayerInfo>();
        protected Dictionary<Node, string> NetworkedTypes = new Dictionary<Node, string>();
        protected Dictionary<Node, string> NetworkedScenes = new Dictionary<Node, string>();

        protected List<Node> OrderedNetworkedNodes = new List<Node>();

        // TODO: Add a way to cleanup scenes that are not used for a while
        protected Dictionary<string, PackedScene> SceneBuffer = new Dictionary<string, PackedScene>();
        protected UPNP ServerUPNP = null;
        protected int UPNPPort;


        public override void _Ready()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Trace));
            this.RegisterCommandAttributes();

            CheckArgsForConnectionInfo();
        }

        /// <summary>
        /// Technically unnecessary but this will trigger the game session delegates so implementations 
        /// won't need to make a special case for offline games
        /// </summary>
        /// <returns>Always returns true</returns>
        [MDCommand]
        public bool StartStandalone()
        {
            MDLog.Info(LOG_CAT, "Starting Standalone Game Session");
            OnPlayerJoined_Internal(STANDALONE_ID);
            MDPlayerInfo PlayerInfo = GetPlayerInfo(STANDALONE_ID);
            if (PlayerInfo != null)
            {
                PlayerInfo.BeginInitialization();
            }
            OnSessionStartedEvent();
            IsSessionStarted = true;
            return true;
        }

        /// <summary>
        /// Starts a multiplayer server
        /// </summary>
        /// <param name="Port">The port to start the server on</param>
        /// <param name="MaxPlayers">Maximum number of players</param>
        /// <returns>True if server is started, false if not</returns>
        [MDCommand(DefaultArgs = new object[] {DEFAULT_PORT, DEFAULT_MAX_PLAYERS})]
        public bool StartServer(int Port, int MaxPlayers = DEFAULT_MAX_PLAYERS)
        {
            NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
            peer.Connect("peer_connected", this, nameof(ServerOnPeerConnected));
            peer.Connect("peer_disconnected", this, nameof(ServerOnPeerDisconnected));
            ConfigurePeer(peer);

            Error error = peer.CreateServer(Port, MaxPlayers);
            bool Success = error == Error.Ok;
            MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info,
                $"Starting server on port {Port} with {MaxPlayers} max players.");
            MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, $"Failed to start server on port {Port}");

            if (Success)
            {
                UPNPPort = Port;
                SetNetworkPeer(peer);
                ServerOnStarted();
            }
            else
            {
                MDLog.Error(LOG_CAT, "Failed to start server");
                OnSessionFailedEvent();
            }

            return Success;
        }

        /// <summary>
        /// Attempts to connect to a multiplayer server
        /// </summary>
        /// <param name="Address">The server address</param>
        /// <param name="Port">The port to connect on</param>
        /// <returns>True on success, false on failure</returns>
        [MDCommand(DefaultArgs = new object[] {DEFAULT_IP, DEFAULT_PORT})]
        public bool StartClient(string Address, int Port)
        {
            NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
            peer.Connect("connection_succeeded", this, nameof(ClientOnConnected));
            peer.Connect("connection_failed", this, nameof(ClientOnFailedToConnect));
            peer.Connect("server_disconnected", this, nameof(ClientOnServerDisconnect));
            ConfigurePeer(peer);

            Error error = peer.CreateClient(Address, Port);
            bool Success = error == Error.Ok;
            MDLog.CLog(Success, LOG_CAT, MDLogLevel.Info, $"Connecting to server at {Address}:{Port}");
            MDLog.CLog(!Success, LOG_CAT, MDLogLevel.Error, $"Failed to connect to server at {Address}:{Port}");

            if (Success)
            {
                SetNetworkPeer(peer);
            }

            return Success;
        }

        private void ConfigurePeer(NetworkedMultiplayerENet Peer)
        {
            Peer.ServerRelay = MDPeerConfigs.ServerRelay;
            Peer.AlwaysOrdered = MDPeerConfigs.AlwaysOrdered;
            Peer.ChannelCount = MDPeerConfigs.ChannelCount;
            Peer.CompressionMode = MDPeerConfigs.CompressionMode;
            Peer.TransferChannel = MDPeerConfigs.TransferChannel;
        }

        /// <summary>
        /// Disconnect from the server if we are connected.
        /// </summary>
        [MDCommand]
        public void Disconnect()
        {
            MDLog.Info(LOG_CAT, "Disconnected from server");
            IsSessionStarted = false;
            foreach (int PeerId in Players.Keys)
            {
                MDPlayerInfo Player = Players[PeerId];
                OnPlayerLeftEvent(PeerId);
                Player.RemoveAndFree();
            }

            NetworkedMultiplayerENet peer = GetPeer();
            if (peer != null)
            {
                peer.CloseConnection();
                SetNetworkPeer(null);
            }

            StopUPNP();
            Players.Clear();
            ClearNetworkedNodes();
            OnSessionEndedEvent();
            SceneBuffer.Clear();
        }

        /// <summary>
        /// Get the underlying network implementation
        /// </summary>
        /// <returns>The underlying ENet implementation or null if not found</returns>
        public NetworkedMultiplayerENet GetPeer()
        {
            NetworkedMultiplayerPeer peer = GameInstance.GetTree().NetworkPeer;
            if (peer is NetworkedMultiplayerENet Net)
            {
                return Net;
            }

            return null;
        }

        private void ServerOnStarted()
        {
            if (GameInstance.UseUPNP())
            {
                ServerUPNP = InitUPNP(UPNPPort);
            }

            MDLog.Info(LOG_CAT, "Server started");
#if !GODOT_SERVER
            OnPlayerJoined_Internal(SERVER_ID);
            MDPlayerInfo PlayerInfo = GetPlayerInfo(SERVER_ID);
            if (PlayerInfo != null)
            {
                PlayerInfo.BeginInitialization();
            }
#endif
            OnSessionStartedEvent();
            IsSessionStarted = true;
        }

        private void ClientOnConnected()
        {
            MDLog.Info(LOG_CAT, "Client connected to server");
            int PeerId = MDStatics.GetPeerId();
            OnPlayerJoined_Internal(PeerId);
            OnSessionStartedEvent();
            IsSessionStarted = true;
        }

        private void ClientOnFailedToConnect()
        {
            MDLog.Error(LOG_CAT, "Client failed to connect to server");
            OnSessionFailedEvent();
        }

        private void ClientOnServerDisconnect()
        {
            MDLog.Info(LOG_CAT, "Client was disconnected from server");
            Disconnect();
        }

        // Called on the server when a client connects
        private void ServerOnPeerConnected(int PeerId)
        {
            MDLog.Info(LOG_CAT, $"Peer [ID: {PeerId}] connected");
            OnPlayerJoined_Internal(PeerId);
            
            MDPlayerInfo PlayerInfo = GetPlayerInfo(PeerId);
            if (PlayerInfo != null)
            {
                PlayerInfo.BeginInitialization();
            }

            SynchronizeCurrentPlayers(PeerId);
            BroadcastNewPlayerJoined(PeerId);
            SynchronizeNetworkedNodes(PeerId);
        }

        // Called on the server when a client disconnects
        private void ServerOnPeerDisconnected(int PeerId)
        {
            MDLog.Info(LOG_CAT, $"Peer [ID: {PeerId}] disconnected");
            OnPlayerLeft_Internal(PeerId);
            BroadcastPlayerLeft(PeerId);
        }

        /// <summary>
        /// Notifies all clients (except the new one, as they'll have received the event already) that a new player has joined
        /// </summary>
        /// <param name="Joiner">The PeerID of the joining client</param>
        protected void BroadcastNewPlayerJoined(int Joiner)
        {
            foreach (int PeerId in Players.Keys)
            {
                if (PeerId == Joiner || PeerId == SERVER_ID)
                {
                    continue;
                }

                MDLog.Debug(LOG_CAT, $"Notifying Peer [{PeerId}] that Peer [{Joiner}] joined");
                RpcId(PeerId, nameof(ClientOnPlayerJoined), Joiner);
            }
        }

        /// <summary>
        /// Notifies all clients that a new player has initialized
        /// </summary>
        /// <param name="Joiner">The PeerID of the joining client</param>
        protected void BroadcastNewPlayerInitialized(int Joiner)
        {
            if (MDStatics.IsNetworkActive())
            {
                foreach (int PeerId in Players.Keys)
                {
                    if (PeerId == SERVER_ID)
                    {
                        continue;
                    }

                    MDLog.Debug(LOG_CAT, $"Notifying Peer [{PeerId}] that Peer [{Joiner}] has initialized");
                    RpcId(PeerId, nameof(OnPlayerInitialized), Joiner);
                }
            }
        }

        /// <summary>
        /// Notifies all clients that a player has left
        /// </summary>
        /// <param name="Leaver">The PeerID of the leaving client</param>
        protected virtual void BroadcastPlayerLeft(int Leaver)
        {
            foreach (int PeerId in Players.Keys)
            {
                if (PeerId == Leaver || PeerId == SERVER_ID)
                {
                    continue;
                }

                MDLog.Debug(LOG_CAT, $"Notifying Peer [{PeerId}] that Peer [{Leaver}] left");
                RpcId(PeerId, nameof(ClientOnPlayerLeft), Leaver);
            }
        }

        private void SynchronizeCurrentPlayers(int Joiner)
        {
            foreach (int PeerId in Players.Keys)
            {
                if (PeerId == Joiner)
                {
                    continue;
                }

                MDPlayerInfo PlayerInfo = GetPlayerInfo(PeerId);
                if (PlayerInfo != null)
                {
                    this.MDRpcId(Joiner, nameof(OnSynchronizePlayer), PeerId, PlayerInfo.HasInitialized);
                }
            }
        }

        [Puppet]
        private void OnSynchronizePlayer(int PeerId, bool IsInitialized)
        {
            ClientOnPlayerJoined(PeerId);
            if (IsInitialized)
            {
                OnPlayerInitialized(PeerId);
            }
        }

        // Called on a client, notifying them that a player joined
        [Puppet]
        private void ClientOnPlayerJoined(int PeerId)
        {
            MDLog.Debug(LOG_CAT, $"Player {PeerId} Joined");
            OnPlayerJoined_Internal(PeerId);
        }

        [Puppet]
        private void OnPlayerInitialized(int PeerId)
        {
            MDPlayerInfo PlayerInfo = GetPlayerInfo(PeerId);
            if (PlayerInfo != null)
            {
                PlayerInfo.HasInitialized = true;
                NotifyPlayerInitializedEvent(PeerId);
                MDLog.Debug(LOG_CAT, $"Player {PeerId} Initialized");
            }
        }

        protected virtual void NotifyPlayerInitializedEvent(int PeerId)
        {
            OnPlayerInitializedEvent(PeerId);
        }

        private void OnPlayerJoined_Internal(int PeerId)
        {
            GetOrCreatePlayerObject(PeerId);
            OnPlayerJoinedEvent(PeerId);
        }

        // Called on a client, notifying them that a player left
        [Puppet]
        private void ClientOnPlayerLeft(int PeerId)
        {
            OnPlayerLeft_Internal(PeerId);
        }

        private void OnPlayerLeft_Internal(int PeerId)
        {
            OnPlayerLeftEvent(PeerId);
            RemovePlayerObject(PeerId);
        }

        // Create and initialize the player object
        private MDPlayerInfo GetOrCreatePlayerObject(int PeerId)
        {
            if (Players.ContainsKey(PeerId))
            {
                return Players[PeerId];
            }

            Type PlayerType = GameInstance.GetPlayerInfoType();
            if (!MDStatics.IsSameOrSubclass(PlayerType, typeof(MDPlayerInfo)))
            {
                MDLog.Error(LOG_CAT, $"Provided player type [{PlayerType.Name}] is not a subclass of MDPlayerInfo");
                return null;
            }

            MDPlayerInfo Player = Activator.CreateInstance(PlayerType) as MDPlayerInfo;
            Player.SetPeerId(PeerId);
            AddChild(Player);
            Players.Add(PeerId, Player);

            OnPlayerInfoCreated(Player);

            return Player;
        }

        /// <summary>
        /// Called on all clients to perform any initialization on a new player info when a player joins, include self
        /// </summary>
        /// <param name="PlayerInfo">The player info for the joining client</param>
        protected virtual void OnPlayerInfoCreated(MDPlayerInfo PlayerInfo)
        {
        }

        /// <summary>
        /// Call this when a Player Info is done initializing
        /// </summary>
        /// <param name="PeerId">The PeerId of the player that completed initialization</param>
        public void OnPlayerInfoInitializationCompleted(int PeerId)
        {
            MDLog.Debug(LOG_CAT, $"Player {PeerId} initialization completed");
            OnPlayerInitialized(PeerId);
            BroadcastNewPlayerInitialized(PeerId);
        }

        /// <summary>
        /// Get the player info for the given per
        /// </summary>
        /// <param name="PeerId">The peer to get the player info for</param>
        /// <returns>The player info or null if the peer is unknown</returns>
        public MDPlayerInfo GetPlayerInfo(int PeerId)
        {
            return Players.ContainsKey(PeerId) ? Players[PeerId] : null;
        }

        /// <summary>
        /// Get player infos for all players
        /// </summary>
        /// <returns>A list of all player infos</returns>
        public List<MDPlayerInfo> GetAllPlayerInfos()
        {
            return new List<MDPlayerInfo>(Players.Values);
        }

        /// <summary>
        /// Returns a list of all active PeerIDs.false This includes the host.
        /// </summary>
        /// <returns></returns>
        public List<int> GetAllPeerIds()
        {
            return new List<int>(Players.Keys);
        }

        /// <summary>
        /// Get the playerinfo for the local player
        /// </summary>
        /// <returns>The player info of the local player</returns>
        public MDPlayerInfo GetMyPlayerInfo()
        {
            return Players[MDStatics.GetPeerId()];
        }

        // Removes the MDPlayerInfo belonging to the PeerId
        private void RemovePlayerObject(int PeerId)
        {
            if (Players.ContainsKey(PeerId))
            {
                MDPlayerInfo Player = Players[PeerId];
                PreparePlayerInfoForRemoval(Player);
                Player.RemoveAndFree();
                Players.Remove(PeerId);
            }
        }

        /// <summary>
        /// Called right before a player info is removed
        /// </summary>
        /// <param name="PlayerInfo">The player info that is going to be removed</param>
        protected virtual void PreparePlayerInfoForRemoval(MDPlayerInfo PlayerInfo)
        {
        }

        // Starts a server or client based on the command args
        private void CheckArgsForConnectionInfo()
        {
            if (MDArguments.HasArg(ARG_STANDALONE))
            {
                StartStandalone();
            }
            // Expects -server=[port]
            else if (MDArguments.HasArg(ARG_SERVER))
            {
                int Port = MDArguments.GetArgInt(ARG_SERVER);
                StartServer(Port);
            }
            // Expects -client=[IPAddress:Port]
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
                    MDLog.Error(LOG_CAT,
                        $"Failed to parse client arg {ClientArg}, expecting -{ARG_CLIENT}=[IPAddress:Port]");
                }
            }
        }

        /// <summary>
        /// Send a command string to the server for execution, only works in debug mode
        /// </summary>
        /// <param name="Command">The command to send</param>
        public void ServerCommand(string Command)
        {
#if DEBUG
            RpcId(GetNetworkMaster(), nameof(ClientToServerCommand), Command);
#endif
        }

        [Master]
        private void ClientToServerCommand(string Command)
        {
#if DEBUG
            MDCommands.InvokeCommand(Command);
#endif
        }

        /// <summary>
        /// Adds a guid to the name if the second boolean parameter is true
        /// </summary>
        /// <param name="Name">The name to randomize</param>
        /// <param name="UseRandomName">Should we randomize it</param>
        /// <returns>Either the name or the name + guid at the end</returns>
        protected string BuildNodeName(string Name, bool UseRandomName)
        {
            if (UseRandomName)
            {
                return Name + Guid.NewGuid();
            }

            return Name;
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="NodeType">The type of node to spawn</param>
        /// <param name="Parent">The parent that the new instance will be a child of</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public Node SpawnNetworkedNode(Type NodeType, Node Parent, string NodeName, bool UseRandomName = true,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            if (this.IsMaster() == false)
            {
                MDLog.Error(LOG_CAT, "Only server can spawn networked nodes");
                return null;
            }

            if (!MDStatics.IsSameOrSubclass(NodeType, typeof(Node)))
            {
                MDLog.Error(LOG_CAT, $"Provided type [{NodeType.Name}] is not a subclass of Node");
                return null;
            }

            if (!Parent.IsInsideTree())
            {
                MDLog.Error(LOG_CAT, $"Parent [{Parent.Name}] is not inside the tree");
                return null;
            }

            NodeName = BuildNodeName(NodeName, UseRandomName);

            int NodeMaster = NetworkMaster != -1 ? NetworkMaster : MDStatics.GetPeerId();
            string NodeTypeString = NodeType.AssemblyQualifiedName;
            string ParentPath = Parent.GetPath();

            Vector3 SpawnPosVal = SpawnPos.GetValueOrDefault();
            if (MDStatics.IsNetworkActive())
            {
                Rpc(nameof(SpawnNodeType), NodeTypeString, ParentPath, NodeName, NodeMaster, SpawnPosVal);
            }

            return SpawnNodeType(NodeTypeString, ParentPath, NodeName, NodeMaster, SpawnPosVal);
        }

        /// <summary>
        /// Spawns a network node
        /// </summary>
        /// <param name="ScenePath">The path to the scene</param>
        /// <param name="Parent">The parent that the new instance will be a child of</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public Node SpawnNetworkedNode(string ScenePath, Node Parent, string NodeName, int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            return SpawnNetworkedNode(ScenePath, Parent, NodeName, true, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Scene">The scene to spawn</param>
        /// <param name="Parent">The parent that the new instance will be a child of</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public Node SpawnNetworkedNode(PackedScene Scene, Node Parent, string NodeName, int NetworkMaster = -1,
            Vector3? SpawnPos = null)
        {
            return SpawnNetworkedNode(Scene.ResourcePath, Parent, NodeName, true, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="Scene">The scene to spawn</param>
        /// <param name="Parent">The parent that the new instance will be a child of</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public Node SpawnNetworkedNode(PackedScene Scene, Node Parent, string NodeName, bool UseRandomName = true,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            return SpawnNetworkedNode(Scene.ResourcePath, Parent, NodeName, UseRandomName, NetworkMaster, SpawnPos);
        }

        /// <summary>
        /// Spawn a network node
        /// </summary>
        /// <param name="ScenePath">The path to the scene to spawn</param>
        /// <param name="Parent">The parent that the new instance will be a child of</param>
        /// <param name="NodeName">The name of the new node</param>
        /// <param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
        /// <param name="NetworkMaster">The peer that should own this, default is server</param>
        /// <param name="SpawnPos">Where the spawn this node</param>
        /// <returns>The new node</returns>
        public Node SpawnNetworkedNode(string ScenePath, Node Parent, string NodeName, bool UseRandomName = true,
            int NetworkMaster = -1, Vector3? SpawnPos = null)
        {
            if (this.IsMaster() == false)
            {
                MDLog.Error(LOG_CAT, "Only server can spawn networked nodes");
                return null;
            }

            if (!Parent.IsInsideTree())
            {
                MDLog.Error(LOG_CAT, $"Parent [{Parent.Name}] is not inside the tree");
                return null;
            }

            NodeName = BuildNodeName(NodeName, UseRandomName);

            int NodeMaster = NetworkMaster != -1 ? NetworkMaster : MDStatics.GetPeerId();
            string ParentPath = Parent.GetPath();

            Vector3 SpawnPosVal = SpawnPos.GetValueOrDefault();
            if (MDStatics.IsNetworkActive())
            {
                Rpc(nameof(SpawnNodeScene), ScenePath, ParentPath, NodeName, NodeMaster, SpawnPosVal);
            }

            return SpawnNodeScene(ScenePath, ParentPath, NodeName, NodeMaster, SpawnPosVal);
        }

        [Puppet]
        private Node SpawnNodeType(string NodeTypeString, string ParentPath, string NodeName, int NetworkMaster,
            Vector3 SpawnPos)
        {
            MDLog.Log(LOG_CAT, MDLogLevel.Debug,
                $"Spawning Node. Type: {NodeTypeString} ParentPath: {ParentPath} Name: {NodeName} Master: {NetworkMaster}");
            Node Parent = GetNodeOrNull(ParentPath);
            if (Parent == null)
            {
                MDLog.Error(LOG_CAT, $"Could not find Parent with path {ParentPath}");
                return null;
            }

            Type NodeType = Type.GetType(NodeTypeString);
            if (NodeType == null)
            {
                MDLog.Error(LOG_CAT, $"Could not find Type {NodeTypeString}");
                return null;
            }

            Node NewNode = Activator.CreateInstance(NodeType) as Node;
            NewNode.Name = NodeName;
            NewNode.SetNetworkMaster(NetworkMaster);
            NetworkedTypes.Add(NewNode, NodeTypeString);
            OrderedNetworkedNodes.Add(NewNode);

            Node2D NewNode2D = NewNode as Node2D;
            Spatial NewNodeSpatial = NewNode as Spatial;
            if (NewNode2D != null)
            {
                NewNode2D.Position = SpawnPos.To2D();
            }
            else if (NewNodeSpatial != null)
            {
                NewNodeSpatial.Translation = SpawnPos;
            }

            Parent.AddChild(NewNode);
            OnNetworkNodeAdded(NewNode);
            return NewNode;
        }

        [Puppet]
        private Node SpawnNodeScene(string ScenePath, string ParentPath, string NodeName, int NetworkMaster,
            Vector3 SpawnPos)
        {
            MDLog.Log(LOG_CAT, MDLogLevel.Debug,
                $"Spawning Node. Scene: {ScenePath} ParentPath: {ParentPath} Name: {NodeName} Master: {NetworkMaster}");
            Node Parent = GetNodeOrNull(ParentPath);
            if (Parent == null)
            {
                MDLog.Error(LOG_CAT, $"Could not find Parent with path: {ParentPath}");
                return null;
            }

            PackedScene Scene = LoadScene(ScenePath);
            if (Scene == null)
            {
                return null;
            }

            Node NewNode = Scene.Instance();
            NewNode.Name = NodeName;
            NewNode.SetNetworkMaster(NetworkMaster);
            NetworkedScenes.Add(NewNode, ScenePath);
            OrderedNetworkedNodes.Add(NewNode);

            Node2D NewNode2D = NewNode as Node2D;
            Spatial NewNodeSpatial = NewNode as Spatial;
            if (NewNode2D != null)
            {
                NewNode2D.Position = SpawnPos.To2D();
            }
            else if (NewNodeSpatial != null)
            {
                NewNodeSpatial.Translation = SpawnPos;
            }

            Parent.AddChild(NewNode);
            OnNetworkNodeAdded(NewNode);
            return NewNode;
        }

        // Allows for buffering of scenes so we don't have to load from disc every time
        private PackedScene LoadScene(string path)
        {
            if (GameInstance.UseSceneBuffer(path))
            {
                if (!SceneBuffer.ContainsKey(path))
                {
                    SceneBuffer[path] = ResourceLoader.Load(path) as PackedScene;
                }

                return SceneBuffer[path];
            }

            // TODO - Support async loading
            return ResourceLoader.Load(path) as PackedScene;
        }

        /// <summary>
        /// Called when a node is removed
        /// </summary>
        /// <param name="RemovedNode">The removed node</param>
        public void OnNodeRemoved(Node RemovedNode)
        {
            if (MDStatics.IsNetworkActive() == false)
            {
                return;
            }

            bool WasNetworked = NetworkedTypes.Remove(RemovedNode);
            if (WasNetworked == false)
            {
                WasNetworked = NetworkedScenes.Remove(RemovedNode);
            }

            if (WasNetworked == false)
            {
                return;
            }

            OrderedNetworkedNodes.Remove(RemovedNode);

            string NodePath = RemovedNode.GetPath();
            OnNetworkNodeRemoved(RemovedNode);
            if (this.IsMaster() && MDStatics.IsNetworkActive())
            {
                Rpc(nameof(RemoveAndFreeNode), NodePath);
            }
        }

        [Puppet]
        private void RemoveAndFreeNode(string NodePath)
        {
            Node NetworkedNode = GetNodeOrNull(NodePath);
            if (NetworkedNode == null)
            {
                MDLog.Error(LOG_CAT, $"Could not find Node with path {NodePath}");
                return;
            }

            OnNetworkNodeRemoved(NetworkedNode);
            NetworkedNode.RemoveAndFree();
        }

        private void SynchronizeNetworkedNodes(int PeerId)
        {
            foreach (Node NetworkedNode in OrderedNetworkedNodes)
            {
                Vector3 SpawnPos = new Vector3();
                Node2D NetworkedNode2D = NetworkedNode as Node2D;
                Spatial NetworkedNodeSpatial = NetworkedNode as Spatial;
                if (NetworkedNode2D != null)
                {
                    SpawnPos = NetworkedNode2D.Position.To3D();
                }
                else if (NetworkedNodeSpatial != null)
                {
                    SpawnPos = NetworkedNodeSpatial.Translation;
                }

                string ParentPath = NetworkedNode.GetParent().GetPath();
                if (NetworkedTypes.ContainsKey(NetworkedNode))
                {
                    string TypePath = NetworkedTypes[NetworkedNode];
                    RpcId(PeerId, nameof(SpawnNodeType), TypePath, ParentPath, NetworkedNode.Name,
                        NetworkedNode.GetNetworkMaster(), SpawnPos);
                }
                else if (NetworkedScenes.ContainsKey(NetworkedNode))
                {
                    string ScenePath = NetworkedScenes[NetworkedNode];
                    RpcId(PeerId, nameof(SpawnNodeScene), ScenePath, ParentPath, NetworkedNode.Name,
                        NetworkedNode.GetNetworkMaster(), SpawnPos);
                }
            }
        }

        private void ClearNetworkedNodes()
        {
            foreach (Node NetworkedNode in OrderedNetworkedNodes.Where(IsInstanceValid))
            {
                NetworkedNode.RemoveAndFree();
            }

            OrderedNetworkedNodes.Clear();
            NetworkedScenes.Clear();
            NetworkedTypes.Clear();
        }

        /// <summary>
        /// Initialize UPNP
        /// </summary>
        /// <param name="Port">The port</param>
        /// <returns>The UPNP object</returns>
        protected UPNP InitUPNP(int Port)
        {
            UPNP NewUPNP = new UPNP();
            UPNP.UPNPResult DiscoverResult = (UPNP.UPNPResult) NewUPNP.Discover();
            MDLog.Info(LOG_CAT, $"UPNP Result for Discover is {DiscoverResult}");
            UPNP.UPNPResult MappingResult = (UPNP.UPNPResult) NewUPNP.AddPortMapping(Port);
            MDLog.Info(LOG_CAT, $"UPNP Result for Mapping Port {Port} is {MappingResult}");
            ExternalAddress = NewUPNP.QueryExternalAddress();
            MDLog.Info(LOG_CAT, $"UPNP External address found [{ExternalAddress}]");
            return NewUPNP;
        }

        /// <summary>
        /// Stops UPNP if it is active
        /// </summary>
        protected void StopUPNP()
        {
            ExternalAddress = "";
            if (ServerUPNP != null)
            {
                ServerUPNP.DeletePortMapping(UPNPPort);
                ServerUPNP.Free();
                ServerUPNP = null;
            }
        }

        /// <summary>
        /// Sets the network peer for the node tree
        /// </summary>
        /// <param name="InPeer">The peer to set to owner of the tree</param>
        protected void SetNetworkPeer(NetworkedMultiplayerPeer InPeer)
        {
            GameInstance.GetTree().NetworkPeer = InPeer;
        }

        /// <summary>
        /// Change the network master of a node. This only works on the server.
        /// </summary>
        /// <param name="Node">The node to change network master of</param>
        /// <param name="NewNetworkMaster">The new network master</param>
        /// <returns>True if network master was changed, false if not</returns>
        public bool ChangeNetworkMaster(Node Node, int NewNetworkMaster)
        {
            if (!GetAllPeerIds().Contains(NewNetworkMaster))
            {
                // Invalid network master
                return false;
            }
            
            // Only server can change network master
            if (MDStatics.IsNetworkActive() && MDStatics.IsServer())
            {
                Node.SetNetworkMaster(NewNetworkMaster);
                Rpc(nameof(ChangeNetworkMasterOnClients), Node.GetPath(), NewNetworkMaster);
                return true;
            }

            return false;
        }

        [Puppet]
        private void ChangeNetworkMasterOnClients(string NodePath, int NewNetworkMaster)
        {
            Node Node = GetNodeOrNull(NodePath);
            if (Node != null)
            {
                Node.SetNetworkMaster(NewNetworkMaster);
            }
        }
    }
}