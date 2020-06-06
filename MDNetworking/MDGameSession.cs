using Godot;
using System;
using System.Collections.Generic;

/*
 * MDGameSession
 *
 * Class that manages the current multiplayer state of the game.
 */
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

	public string ExternalAddress { get; protected set; } = "";

	public delegate void PlayerEventHandler(int PeerId);
	// Triggered whenever a player joined, includes self and existing players when initially joining
	public event PlayerEventHandler OnPlayerJoinedEvent = delegate {};
	public event PlayerEventHandler OnPlayerLeftEvent = delegate {};
	public event PlayerEventHandler OnPlayerNameChanged = delegate {};

	public delegate void SessionEventHandler();
	public event SessionEventHandler OnSessionStartedEvent = delegate {};
	public event SessionEventHandler OnSessionFailedEvent = delegate {};
	public event SessionEventHandler OnSessionEndedEvent = delegate {};

	public MDReplicator Replicator {get; private set;} = new MDReplicator();
	protected Dictionary<int, MDPlayerInfo> Players = new Dictionary<int, MDPlayerInfo>();
	protected Dictionary<Node, string> NetworkedTypes = new Dictionary<Node, string>();
	protected Dictionary<Node, string> NetworkedScenes = new Dictionary<Node, string>();
	protected List<Node> OrderedNetworkedNodes = new List<Node>();
	protected Dictionary<String, PackedScene> SceneBuffer = new Dictionary<String, PackedScene>();
	protected UPNP ServerUPNP = null;
	protected int UPNPPort;

	protected RandomNumberGenerator Random = new RandomNumberGenerator();


	public override void _Ready()
	{
		MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
		this.RegisterCommandAttributes();

		CheckArgsForConnectionInfo();

		GetTree().Connect("idle_frame", this, nameof(PreTick));
	}

	private void PreTick()
	{
		Replicator.TickReplication();
	}

	// Technically unnecessary but this will trigger the game session delegates so implementations won't need to make a special case for offline games
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
			UPNPPort = Port;
			SetNetworkPeer(peer);
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
			SetNetworkPeer(peer);
		}

		return Success;
	}

	[MDCommand()]
	public void Disconnect()
	{
		OnDisconnect();
	}

	public NetworkedMultiplayerENet GetPeer()
	{
		NetworkedMultiplayerPeer peer = GameInstance.GetTree().NetworkPeer;
		if (peer is NetworkedMultiplayerENet)
		{
			return (NetworkedMultiplayerENet)peer;
		}

		return null;
	}

	private void StandaloneOnStarted()
	{
		OnPlayerJoined_Internal(STANDALONE_ID);
		OnSessionStartedEvent();
		IsSessionStarted = true;
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
		#endif
		OnSessionStartedEvent();
		IsSessionStarted = true;
	}
	private void ServerOnFailedToStart()
	{
		MDLog.Error(LOG_CAT, "Failed to start server");
		OnSessionFailedEvent();
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
		MDLog.Error(LOG_CAT, "Client was disconnected from server");
		ClientOnDisconnectedFromServer();
	}

	// Called on the server when a client connects
	private void ServerOnPeerConnected(int PeerId)
	{
		MDLog.Info(LOG_CAT, "Peer [ID: {0}] connected", PeerId);
		OnPlayerJoined_Internal(PeerId);
		BroadcastNewPlayerJoined(PeerId);
		SendConnectionDataToClient(PeerId);
		SynchronizeNetworkedNodes(PeerId);
		Replicator.OnPlayerJoined(PeerId);
	}

	// Called on the server when a client disconnects
	private void ServerOnPeerDisconnected(int PeerId)
	{
		MDLog.Info(LOG_CAT, "Peer [ID: {0}] disconnected", PeerId);
		OnPlayerLeft_Internal(PeerId);
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
				if (PeerId == MDStatics.GetPeerId())
				{
					Players[PeerId].PerformFullSync(Joiner);
				}
				else
				{
					RpcId(PeerId, nameof(ClientSendConnectionDataToClient), Joiner);
				}
			}
		}
	}

	// Tells the network master of a player info to sync a new player
	[Puppet]
	protected virtual void ClientSendConnectionDataToClient(int Joiner)
	{
		GetPlayerInfo(MDStatics.GetPeerId()).PerformFullSync(Joiner);
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
		OnPlayerJoined_Internal(PeerId);
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
		RemovePlayerObject(PeerId);
		OnPlayerLeftEvent(PeerId);
	}

	// Called when disconnected from the server or as the server
	private void OnDisconnect()
	{
		IsSessionStarted = false;
		foreach (int PeerId in Players.Keys)
		{
			MDPlayerInfo Player = Players[PeerId];
			Player.RemoveAndFree();
			OnPlayerLeftEvent(PeerId);
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
			MDLog.Error(LOG_CAT, "Provided player type [{0}] is not a subclass of MDPlayerInfo", PlayerType.Name);
			return null;
		}

		MDPlayerInfo Player = Activator.CreateInstance(PlayerType) as MDPlayerInfo;
		Player.InitPlayerInfo(PeerId);
		InitializePlayerInfo(Player);
		AddChild(Player);

		Players.Add(PeerId, Player);
		return Player;
	}

	protected virtual void InitializePlayerInfo(MDPlayerInfo PlayerInfo)
	{
	}

	public MDPlayerInfo GetPlayerInfo(int PeerId)
	{
		if (Players.ContainsKey(PeerId))
		{
			return Players[PeerId];
		}

		return null;
	}

	public List<MDPlayerInfo> GetAllPlayerInfos()
	{
		return new List<MDPlayerInfo>(Players.Values);
	}

	public MDPlayerInfo GetMyPlayerInfo()
	{
		foreach (MDPlayerInfo info in Players.Values)
		{
			if (info.IsNetworkMaster())
			{
				return info;
			}
		}
		return null;
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
				MDLog.Error(LOG_CAT, "Failed to parse client arg {0}, expecting -{1}=[IPAddres:Port]", ClientArg, ARG_CLIENT);
			}
		}
	}

	public void ServerCommand(string Command)
	{
		#if DEBUG
		RpcId(this.GetNetworkMaster(), nameof(ClientToServerCommand), Command);
		#endif
	}

	[Master]
	private void ClientToServerCommand(string Command)
	{
		#if DEBUG
		MDCommands.InvokeCommand(Command);
		#endif
	}

	///<summary>If set all network nodes will get a random number appended to them</summary>
	public virtual bool UseRandomName()
	{
		return true;
	}

	protected String GetNodeName(String name)
	{
		if (UseRandomName())
		{
			return name + Random.RandiRange(0, Int32.MaxValue);
		}

		return name;
	}

	public Node SpawnNetworkedNode(Type NodeType, Node Parent, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
	{
		if (this.IsMaster() == false)
		{
			MDLog.Error(LOG_CAT, "Only server can spawn networked nodes");
			return null;
		}

		if (!MDStatics.IsSameOrSubclass(NodeType, typeof(Node)))
		{
			MDLog.Error(LOG_CAT, "Provided type [{0}] is not a subclass of Node", NodeType.Name);
			return null;
		}

		if (!Parent.IsInsideTree())
		{
			MDLog.Error(LOG_CAT, "Parent [{0}] is not inside the tree", Parent.Name);
			return null;
		}

		NodeName = GetNodeName(NodeName);

		int NodeMaster = (NetworkMaster != -1) ? NetworkMaster : MDStatics.GetPeerId();
		string NodeTypeString = NodeType.AssemblyQualifiedName;
		string ParentPath = Parent.GetPath();

		Vector3 SpawnPosVal = SpawnPos.GetValueOrDefault();
		if (MDStatics.IsNetworkActive())
		{
			Rpc(nameof(SpawnNodeType), NodeTypeString, ParentPath, NodeName, NodeMaster, SpawnPosVal);
		}

		return SpawnNodeType(NodeTypeString, ParentPath, NodeName, NodeMaster, SpawnPosVal);
	}

	public Node SpawnNetworkedNode(PackedScene Scene, Node Parent, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
	{
		return SpawnNetworkedNode(Scene.ResourcePath, Parent, NodeName, NetworkMaster, SpawnPos);
	}

	public Node SpawnNetworkedNode(string ScenePath, Node Parent, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
	{
		if (this.IsMaster() == false)
		{
			MDLog.Error(LOG_CAT, "Only server can spawn networked nodes");
			return null;
		}

		if (!Parent.IsInsideTree())
		{
			MDLog.Error(LOG_CAT, "Parent [{0}] is not inside the tree", Parent.Name);
			return null;
		}
		
		NodeName = GetNodeName(NodeName);

		int NodeMaster = (NetworkMaster != -1) ? NetworkMaster : MDStatics.GetPeerId();
		string ParentPath = Parent.GetPath();

		Vector3 SpawnPosVal = SpawnPos.GetValueOrDefault();
		if (MDStatics.IsNetworkActive())
		{
			Rpc(nameof(SpawnNodeScene), ScenePath, ParentPath, NodeName, NodeMaster, SpawnPosVal);
		}

		return SpawnNodeScene(ScenePath, ParentPath, NodeName, NodeMaster, SpawnPosVal);
	}

	[Puppet]
	private Node SpawnNodeType(string NodeTypeString, string ParentPath, string NodeName, int NetworkMaster, Vector3 SpawnPos)
	{
		MDLog.Log(LOG_CAT, MDLogLevel.Debug, "Spawning Node. Type: {0} ParentPath: {1} Name: {2} Master: {3}", NodeTypeString, ParentPath, NodeName, NetworkMaster);
		Node Parent = GetNodeOrNull(ParentPath);
		if (Parent == null)
		{
			MDLog.Error(LOG_CAT, "Could not find Parent with path {0}", ParentPath);
			return null;
		}

		Type NodeType = Type.GetType(NodeTypeString);
		if (NodeType == null)
		{
			MDLog.Error(LOG_CAT, "Could not find Type {0}", NodeTypeString);
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
		return NewNode;
	}

	[Puppet]
	private Node SpawnNodeScene(string ScenePath, string ParentPath, string NodeName, int NetworkMaster, Vector3 SpawnPos)
	{
		MDLog.Log(LOG_CAT, MDLogLevel.Debug, "Spawning Node. Scene: {0} ParentPath: {1} Name: {2} Master: {3}", ScenePath, ParentPath, NodeName, NetworkMaster);
		Node Parent = GetNodeOrNull(ParentPath);
		if (Parent == null)
		{
			MDLog.Error(LOG_CAT, "Could not find Parent with path", ParentPath);
			return null;
		}

		// TODO - Support async loading
		PackedScene Scene = LoadScene(ScenePath);
		if (Scene != null)
		{
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
			return NewNode;
		}

		return null;
	}

	///<summary>Allows for buffering of scenes so we don't have to load from disc every time</summary>
	private PackedScene LoadScene(String path)
	{
		if (UseSceneBuffer())
		{
			if (!SceneBuffer.ContainsKey(path))
			{
				SceneBuffer[path] = ResourceLoader.Load(path) as PackedScene;
			}
			return SceneBuffer[path];
		}

		return ResourceLoader.Load(path) as PackedScene;
	}

	///<summary>If true we will keep a reference to all loaded scenes around so we don't need to load the resource from disc every time</summary>
	protected virtual bool UseSceneBuffer()
	{
		return true;
	}

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
			MDLog.Error(LOG_CAT, "Could not find Node with path {0}", NodePath);
			return;
		}

		NetworkedNode.RemoveAndFree();
	}

	private void SynchronizeNetworkedNodes(int PeerId)
	{
		foreach(Node NetworkedNode in OrderedNetworkedNodes)
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
				RpcId(PeerId, nameof(SpawnNodeType), TypePath, ParentPath, NetworkedNode.Name, NetworkedNode.GetNetworkMaster(), SpawnPos);
			}
			else if (NetworkedScenes.ContainsKey(NetworkedNode))
			{
				string ScenePath = NetworkedScenes[NetworkedNode];
				RpcId(PeerId, nameof(SpawnNodeScene), ScenePath, ParentPath, NetworkedNode.Name, NetworkedNode.GetNetworkMaster(), SpawnPos);
			}
		}
	}

	private void ClearNetworkedNodes()
	{
		foreach(Node NetworkedNode in OrderedNetworkedNodes)
		{
			NetworkedNode.RemoveAndFree();
		}

		OrderedNetworkedNodes.Clear();
		NetworkedScenes.Clear();
		NetworkedTypes.Clear();
	}

	protected UPNP InitUPNP(int Port)
	{
		UPNP NewUPNP = new UPNP();
		UPNP.UPNPResult DiscoverResult = (UPNP.UPNPResult)NewUPNP.Discover();
		MDLog.Info(LOG_CAT, "UPNP Result for Discover is {0}", DiscoverResult);
		UPNP.UPNPResult MappingResult = (UPNP.UPNPResult)NewUPNP.AddPortMapping(Port);
		MDLog.Info(LOG_CAT, "UPNP Result for Mapping Port {0} is {1}", Port, MappingResult);
		ExternalAddress = NewUPNP.QueryExternalAddress();
		MDLog.Info(LOG_CAT, "UPNP External address found [{0}]", ExternalAddress);
		return NewUPNP;
	}

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

	protected void SetNetworkPeer(NetworkedMultiplayerPeer InPeer)
	{
		GameInstance.GetTree().NetworkPeer = InPeer;
	}

	public void NotifyPlayerNameChanged(int peerId)
	{
		OnPlayerNameChanged(peerId);
	}
}
