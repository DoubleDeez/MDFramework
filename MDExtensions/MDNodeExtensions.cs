using Godot;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/*
 * MDNodeExtensions
 *
 * Extension class to provide useful framework methods
 */
public static class MDNodeExtensions
{
    // Grabs the singleton game instance
    public static MDGameInstance GetGameInstance(this Node Instance)
    {
        return Instance.GetNode("/root/GameInstance") as MDGameInstance;
    }

    /// <summary>Grabs the GameSession from the GameInstance</summary>
    public static MDGameSession GetGameSession(this Node Instance)
    {
        MDGameInstance GI = Instance.GetGameInstance();
        return GI.GameSession;
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

    public static MDInput GetInputState(this Node Instance)
    {
        return Instance.GetGameInstance().InputState;
    }

    public static T GetPlayerInfo<T>(this Node Instance, int PeerId) where T : MDPlayerInfo
    {
        MDGameSession Session = Instance.GetGameSession();
        MDPlayerInfo PlayerInfo = Session.GetPlayerInfo(PeerId);
        return PlayerInfo as T;
    }
    ///<param name="NodeType">The type of node to spawn</param>
    ///<param name="NodeName">The name of the new node</param>
    ///<param name="NetworkMaster">The peer that should own this, default is server</param>
    ///<param name="SpawnPos">Where the spawn this node</param>
    public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, true, NetworkMaster, SpawnPos);
    }
    ///<param name="NodeType">The type of node to spawn</param>
    ///<param name="NodeName">The name of the new node</param>
    ///<param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
    ///<param name="NetworkMaster">The peer that should own this, default is server</param>
    ///<param name="SpawnPos">Where the spawn this node</param>
    public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName, bool UseRandomName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
    }
    ///<param name="Scene">The packed scene to spawn</param>
    ///<param name="NodeName">The name of the new node</param>
    ///<param name="NetworkMaster">The peer that should own this, default is server</param>
    ///<param name="SpawnPos">Where the spawn this node</param>
    public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, NetworkMaster, SpawnPos);
    }
    ///<param name="Scene">The packed scene to spawn</param>
    ///<param name="NodeName">The name of the new node</param>
    ///<param name="UseRandomName">If set to true a random number will be added at the end of the node name</param>
    ///<param name="NetworkMaster">The peer that should own this, default is server</param>
    ///<param name="SpawnPos">Where the spawn this node</param>
    public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName, bool UseRandomName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, UseRandomName, NetworkMaster, SpawnPos);
    }
    ///<param name="ScenePath">The path to the scene</param>
    ///<param name="NodeName">The name of the new node</param>
    ///<param name="NetworkMaster">The peer that should own this, default is server</param>
    ///<param name="SpawnPos">Where the spawn this node</param>
    public static Node SpawnNetworkedNode(this Node Instance, string ScenePath, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(ScenePath, Instance, NodeName, NetworkMaster, SpawnPos);
    }

    // Shortcut for GetTree().GetRoot().AddChild()
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

    // Helper to mark input as handled
    public static void SetInputHandled(this Node Instance)
    {
        Instance.GetTree().SetInputAsHandled();
    }

    // Helper to register commands
    public static void RegisterCommandAttributes(this Node Instance)
    {
        MDCommands.RegisterCommandAttributes(Instance);
    }

    // Helper to unregister commands
    public static void UnregisterCommandAttributes(this Node Instance)
    {
        MDCommands.UnregisterCommandAttributes(Instance);
    }

    // Helper to register replication
    public static void RegisterReplicatedAttributes(this Node Instance)
    {
        Instance.GetGameSession().Replicator.RegisterReplication(Instance);
    }

    // Helper to unregister replication
    public static void UnregisterReplicatedAttributes(this Node Instance)
    {
        Instance.GetGameSession().Replicator.UnregisterReplication(Instance);
    }

    // Helper to populate members marked with [MDBindNode()]
    public static void PopulateBindNodes(this Node Instance)
    {
        MDBindNode.PopulateBindNodes(Instance);
    }

    // Returns true if this application can set replicated variables, call client RPCs, and broadcast RPCs
    public static bool IsServer(this Node Instance)
    {
        return Instance.GetNetMode() < MDNetMode.Client;
    }

    // Returns true if the local peer is the network master of the node or we're not networking
    public static bool IsMaster(this Node Instance)
    {
        return MDStatics.IsNetworkActive() == false || Instance.GetNetworkMaster() == MDStatics.GetPeerId();
    }

    ///<summary>Returns true if we are the client on a network</summary>
    public static bool IsClient(this Node Instance)
    {
        return MDStatics.IsClient();
    }

    // Returns the net mode of the game session
    public static MDNetMode GetNetMode(this Node Instance)
    {
        if (Instance.GetTree().HasNetworkPeer()) {
            if (Instance.GetTree().IsNetworkServer()) {
                return MDNetMode.Server;
            }

            return MDNetMode.Client;
        }

        return MDNetMode.Standalone;
    }

    // Removes this node from its parent and frees it
    public static void RemoveAndFree(this Node Instance)
    {
        Instance.GetParent().RemoveChild(Instance);
        Instance.QueueFree();
    }

    // Same as Rpc except it checks if the network is activate first
    public static object MDRpc(this Node Instance, string Method, params object[] Args)
    {
        if (MDStatics.IsNetworkActive())
        {
            return Instance.Rpc(Method, Args);
        }

        return null;
    }

    // Same as RpcId except it checks if the network is activate first
    public static object MDRpcId(this Node Instance, int PeerId, string Method, params object[] Args)
    {
        if (MDStatics.IsNetworkActive())
        {
            return Instance.RpcId(PeerId, Method, Args);
        }

        return null;
    }

    // Same as RpcUnreliable except it checks if the network is activate first
    public static object MDRpcUnreliable(this Node Instance, string Method, params object[] Args)
    {
        if (MDStatics.IsNetworkActive())
        {
            return Instance.RpcUnreliable(Method, Args);
        }

        return null;
    }

    // Same as RpcUnreliableId except it checks if the network is activate first
    public static object MDRpcUnreliableId(this Node Instance, int PeerId, string Method, params object[] Args)
    {
        if (MDStatics.IsNetworkActive())
        {
            return Instance.RpcUnreliableId(PeerId, Method, Args);
        }

        return null;
    }

    // Same as Rset except it checks if the network is activate first
    public static void MDRset(this Node Instance, string Property, object Value)
    {
        if (MDStatics.IsNetworkActive())
        {
            Instance.Rset(Property, Value);
        }
    }

    // Same as RsetId except it checks if the network is activate first
    public static void MDRsetId(this Node Instance, int PeerId, string Property, object Value)
    {
        if (MDStatics.IsNetworkActive())
        {
            Instance.RsetId(PeerId, Property, Value);
        }
    }

    // Same as RsetUnreliable except it checks if the network is activate first
    public static void MDRsetUnreliable(this Node Instance, string Property, object Value)
    {
        if (MDStatics.IsNetworkActive())
        {
            Instance.RsetUnreliable(Property, Value);
        }
    }

    // Same as RsetUnreliable except it checks if the network is activate first
    public static void MDRsetUnreliableId(this Node Instance, int PeerId, string Property, object Value)
    {
        if (MDStatics.IsNetworkActive())
        {
            Instance.RsetUnreliableId(PeerId, Property, Value);
        }
    }

    // Sends the RPC to the server only
    public static object MDServerRpc(this Node Instance, string Method, params object[] Args)
    {
        int ServerId = Instance.GetGameSession().GetNetworkMaster();
        return Instance.MDRpcId(ServerId, Method, Args);
    }

    // Sends the unreliable RPC to the server only
    public static object MDServerRpcUnreliable(this Node Instance, string Method, params object[] Args)
    {
        int ServerId = Instance.GetGameSession().GetNetworkMaster();
        return Instance.MDRpcUnreliableId(ServerId, Method, Args);
    }

    ///<summary>Creates a timer as a child of the current node</summary>
    ///<param name="Name">The name of the timer</param>
    ///<param name="OneShot">Is this a one shot timer</param>
    ///<param name="WaitTime">Duration of the timer</param>
    ///<param name="TimerAsFirstArgument">Should we pass the timer as the first argument to the timeout method?</param>
    ///<param name="ConnectionTarget">The object to attach the timeout method to</param>
    ///<param name="MethodName">The name of the timeout method</param>
    ///<param name="Parameters">Array of parameters to pass to the timeout function</param>
    public static Timer CreateTimer(this Node Instance, String Name, bool OneShot, float WaitTime, bool TimerAsFirstArgument, Godot.Object ConnectionTarget, String MethodName, params object[] Parameters)
    {
        Timer timer = new Timer();
        timer.Name = Name;
        timer.OneShot = OneShot;
        timer.WaitTime = WaitTime;
        List<object> parameters = new List<object>();
        if (TimerAsFirstArgument)
        {
            parameters.Add(timer);
        }
        foreach (object param in Parameters)
        {
            parameters.Add(param);
        }
        timer.Connect("timeout", ConnectionTarget, MethodName, new Godot.Collections.Array(parameters));
        Instance.AddChild(timer);
        return timer;
    }

}
