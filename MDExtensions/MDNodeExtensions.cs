using Godot;
using System;
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

    // Grabs the GameSession from the GameInstance
    public static MDGameSession GetGameSession(this Node Instance)
    {
        MDGameInstance GI = Instance.GetGameInstance();
        return GI.GameSession;
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
    
    public static Node SpawnNetworkedNode(this Node Instance, Type NodeType, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(NodeType, Instance, NodeName, NetworkMaster, SpawnPos);
    }
    public static Node SpawnNetworkedNode(this Node Instance, PackedScene Scene, string NodeName, int NetworkMaster = -1, Vector3? SpawnPos = null)
    {
        MDGameSession GameSession = Instance.GetGameSession();
        return GameSession.SpawnNetworkedNode(Scene, Instance, NodeName, NetworkMaster, SpawnPos);
    }
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

}