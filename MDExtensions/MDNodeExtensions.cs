using Godot;
using System;

/*
 * MDNodeExtensions
 *
 * Extension class to provide useful framework methods
 */
public static class MDNodeExtensions
{
    // Grabs the singleton game instance
    public static MDGameInstance GetGameInstance(this Node instance)
    {
        return instance.GetNode("/root/MDGameInstance") as MDGameInstance;
    }

    // Grabs the GameSession from the GameInstance
    public static MDGameSession GetGameSession(this Node instance)
    {
        MDGameInstance GI = instance.GetGameInstance();
        return GI.GetGameSession();
    }

    // Shortcut for GetTree().GetRoot().AddChild()
    public static void AddNodeToRoot(this Node instance, Node Child, bool Deferred = false)
    {
        if (Deferred)
        {
            instance.GetTree().GetRoot().CallDeferred("add_child", Child);
        }
        else
        {
            instance.GetTree().GetRoot().AddChild(Child);
        }
    }

    // Helper to mark input as handled
    public static void SetInputHandled(this Node instance)
    {
        instance.GetTree().SetInputAsHandled();
    }
}