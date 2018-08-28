using Godot;
using System;

/*
 * MDGameInstance
 *
 * Single-instance class that persists throughout the life-time of the game application.
 */
public class MDGameInstance : Node
{
    public override void _Ready()
    {
        MDLog.Initialize();
        MDArguments.PopulateArgs();
        CreateGameSession();
        CreateInterfaceManager();
    }
    
    public MDGameSession GetGameSession()
    {
        return GameSession;
    }

    private void CreateGameSession()
    {
        if (GameSession == null)
        {
            GameSession = new MDGameSession();
            GameSession.SetName("GameSession");
            this.AddNodeToRoot(GameSession, true);
        }
    }

    private void CreateInterfaceManager()
    {
        if (InterfaceManager == null)
        {
            InterfaceManager = new MDInterfaceManager();
            InterfaceManager.SetName("InterfaceManager");
            this.AddNodeToRoot(InterfaceManager, true);
        }
    }

    private MDGameSession GameSession = null;
    private MDInterfaceManager InterfaceManager = null;
}
