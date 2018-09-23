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
        Instance.RegisterReplicatedFields();
        Instance.RegisterRPCs();
    }

    // Unregisters a removed node from MDFramework systems
    private void UnregisterNode(Node Instance)
    {
        // We automatically unregister commands even though we don't automatically register them to avoid relying on the user to do so
        Instance.UnregisterCommandAttributes();
        Instance.UnregisterReplicatedFields();
        Instance.UnregisterRPCs();
    }

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        GameSession.Replicator.RegisterReplication(Instance);
    }

    // Unregisters the given instance's fields marked with [MDReplicated()]
    public void UnregisterReplication(Node Instance)
    {
        GameSession.Replicator.UnregisterReplication(Instance);
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
