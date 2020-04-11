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

    public static T GetPlayerInfo<T>(this Node Instance, int PeerId) where T : MDPlayerInfo
    {
        MDGameSession Session = Instance.GetGameSession();
        MDPlayerInfo PlayerInfo = Session.GetPlayerInfo(PeerId);
        return PlayerInfo as T;
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

    // Returns true if the local peer is the network master of the node (or we're not networks)
    public static bool IsNetworkMaster(this Node Instance)
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
}