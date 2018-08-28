using Godot;
using System;

/*
 * MDNode
 *
 * Base class for our main framework classes.
 */
public class MDNode : Node
{
    // Grabs the singleton game instance
    public MDGameInstance GetGameInstance()
    {
        if (GameInstance == null)
        {
            GameInstance = GetNode("/root/MDGameInstance") as MDGameInstance;
        }

        return GameInstance;
    }

    // Grabs the GameSession instance from the GameInstance
    public MDGameSession GetGameSession()
    {
        MDGameInstance GI = GetGameInstance();
        return GI.GetGameSession();
    }

    // Shortcut for GetTree().GetRoot().AddChild()
    public void AddNodeToRoot(Node Child, bool Deferred = false)
    {
        if (Deferred)
        {
            GetTree().GetRoot().CallDeferred("AddChild", Child);
        }
        else
        {
            GetTree().GetRoot().AddChild(Child);
        }
    }
    
    // Cached GameInstance reference
    private MDGameInstance GameInstance = null;
}