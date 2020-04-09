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

        // Hook up events
        GetTree().Connect("node_added", this, nameof(OnNodeAdded_Internal));
        GetTree().Connect("node_removed", this, nameof(OnNodeRemoved_Internal));

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

    // Override this to provide the your GameSession subclass type
    protected virtual Type GetGameSessionType()
    {
        return typeof(MDGameSession);
    }

    // Called whenever a node is added to the scene
    protected virtual void OnNodeAdded(Node AddedNode)
    {

    }

    // Called whenever a node is removed to the scene
    protected virtual void OnNodeRemoved(Node RemovedNode)
    {

    }

    // Bound to SceneTree.node_added
    private void OnNodeAdded_Internal(Godot.Object NodeObj)
    {
        Node AddedNode = NodeObj as Node;
        if (AddedNode != null)
        {
            RegisterNewNode(AddedNode);
            OnNodeAdded(AddedNode);
        }
    }

    // Bound to SceneTree.node_removed
    private void OnNodeRemoved_Internal(Godot.Object NodeObj)
    {
        Node RemovedNode = NodeObj as Node;
        if (RemovedNode != null)
        {
            UnregisterNode(RemovedNode);
            OnNodeRemoved(RemovedNode);
        }
    }

    // Registers a new node to MDFramework systems
    private void RegisterNewNode(Node Instance)
    {
        Instance.PopulateBindNodes();
    }

    // Unregisters a removed node from MDFramework systems
    private void UnregisterNode(Node Instance)
    {
        // We automatically unregister commands even though we don't automatically register them to avoid relying on the user to do so
        Instance.UnregisterCommandAttributes();
    }

    // Ensure GameSession is created
    private void CreateGameSession()
    {
        Type GSType = GetGameSessionType();
        if (!MDStatics.IsSameOrSubclass(GSType, typeof(MDGameSession)))
        {
            MDLog.Error(LOG_CAT, "Provided game session type [{0}] is not a subclass of MDGameSession", GSType.Name);
            return;
        }

        if (GameSession == null)
        {
            GameSession = Activator.CreateInstance(GSType) as MDGameSession;
            GameSession.Name = "GameSession";
            this.AddNodeToRoot(GameSession, true);
        }
    }

    // Ensure InterfaceManager is created
    private void CreateInterfaceManager()
    {
        if (InterfaceManager == null)
        {
            InterfaceManager = new MDInterfaceManager();
            InterfaceManager.Name = "InterfaceManager";
            this.AddNodeToRoot(InterfaceManager, true);
        }
    }

    public MDGameSession GameSession {get; private set;}
    public MDInterfaceManager InterfaceManager {get; private set;}
}
