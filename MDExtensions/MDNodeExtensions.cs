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

    // Shortcut for GetTree().GetRoot().AddChild()
    public static void AddNodeToRoot(this Node Instance, Node Child, bool Deferred = false)
    {
        if (Deferred)
        {
            Instance.GetTree().GetRoot().CallDeferred("add_child", Child);
        }
        else
        {
            Instance.GetTree().GetRoot().AddChild(Child);
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

    // Helper to register all replicated variables on the replicator
    public static void RegisterReplicatedFields(this Node Instance)
    {
        Instance.GetGameInstance().RegisterReplication(Instance);
    }

    // Helper to register all RPC functions on the remote caller
    public static void RegisterRPCs(this Node Instance)
    {
        Instance.GetGameSession().RegisterRPCs(Instance);
    }

    // Extension to call RPC functions
    public static void CallRPC(this Node Instance, string FunctionName, params object[] args)
    {
        Instance.GetGameSession().CallRPC(Instance, FunctionName, args);
    }

    // Returns true if this application can set replicated variables, call client RPCs, and broadcast RPCs
    public static bool HasNetworkAuthority(this Node Instance)
    {
        return Instance.GetNetMode() < MDNetMode.Client;
    }

    // Returns the net mode of the game session
    public static MDNetMode GetNetMode(this Node Instance)
    {
        return Instance.GetGameSession().NetEntity.NetMode;
    }

    // Registers this classes MDRpc() methods
    public static void SetNetOwner(this Node Instance)
    {
        Instance.GetGameSession().RegisterRPCs(Instance);
    }
}