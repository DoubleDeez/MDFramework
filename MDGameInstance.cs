using Godot;
using System;

/*
 * MDGameInstance
 *
 * Single-instance class that persists throughout the life-time of the game application.
 */
public class MDGameInstance : MDNode
{
    public override void _Ready()
    {
        MDLog.Initialize();
        MDArguments.PopulateArgs();
        CreateGameSession();
    }
    
    public new MDGameSession GetGameSession()
    {
        return GameSession;
    }

    private void CreateGameSession()
    {
        if (GameSession == null)
        {
            GameSession = new MDGameSession();
            GameSession.SetName("GameSession");
            AddNodeToRoot(GameSession, true);
        }
    }

    private MDGameSession GameSession = null;
}
