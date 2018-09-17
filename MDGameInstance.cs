using Godot;
using System;

/*
 * MDGameInstance
 *
 * Single-instance class that persists throughout the life-time of the game application.
 */
public class MDGameInstance : Node
{
    private const string LOG_CAT = "LogGameInstance";

    public override void _Ready()
    {
        // Init static classes first
        MDStatics.GI = this;
        MDLog.Initialize();
        MDArguments.PopulateArgs();

        // Init instances
        CreateGameSession();
        CreateInterfaceManager();
    }

    public override void _Notification(int NotificationType)
    {
        base._Notification(NotificationType);

        switch(NotificationType)
        {
            case MainLoop.NotificationWmQuitRequest:
                MDLog.Info(LOG_CAT, "Quit notification received.");
                if (GameSession != null)
                {
                    GameSession.Disconnect();
                }
                break;
        }
    }

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        GameSession.Replicator.RegisterReplication(Instance);
    }

    // Ensure GameSession is created
    private void CreateGameSession()
    {
        if (GameSession == null)
        {
            GameSession = new MDGameSession();
            GameSession.SetName("GameSession");
            this.AddNodeToRoot(GameSession, true);
        }
    }

    // Ensure InterfaceManager is created
    private void CreateInterfaceManager()
    {
        if (InterfaceManager == null)
        {
            InterfaceManager = new MDInterfaceManager();
            InterfaceManager.SetName("InterfaceManager");
            this.AddNodeToRoot(InterfaceManager, true);
        }
    }

    public MDGameSession GameSession {get; private set;}
    public MDInterfaceManager InterfaceManager {get; private set;}
}
