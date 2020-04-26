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
        MDProfiler.Initialize();

        // Hook up events
        GetTree().Connect("node_added", this, nameof(OnNodeAdded_Internal));
        GetTree().Connect("node_removed", this, nameof(OnNodeRemoved_Internal));

        // Init instances
        CreateGameSession();
        CreateInterfaceManager();

        RegisterNodeAndChildren(GetTree().Root);
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

    // Override this to provide your own Player class type
    public virtual Type GetPlayerInfoType()
    {
        return typeof(MDPlayerInfo);
    }

    // Called whenever a node is added to the scene
    protected virtual void OnNodeAdded(Node AddedNode)
    {

    }

    // Called whenever a node is removed to the scene
    protected virtual void OnNodeRemoved(Node RemovedNode)
    {

    }

    // Travels the tree and registers the existing nodes
    private void RegisterNodeAndChildren(Node RootNode)
    {
        if (RootNode != null)
        {
            OnNodeAdded_Internal(RootNode);

            int ChildCount = RootNode.GetChildCount();
            for (int i = 0; i < ChildCount; ++i)
            {
                RegisterNodeAndChildren(RootNode.GetChild(i));
            }
        }
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
            if (GameSession != null && GameSession != RemovedNode)
            {
                GameSession.OnNodeRemoved(RemovedNode);
            }
        }
    }

    // Registers a new node to MDFramework systems
    private void RegisterNewNode(Node Instance)
    {
        bool RegisterDebug = false;
        MDAutoRegister AutoRegAtr = MDStatics.FindClassAttribute<MDAutoRegister>(Instance.GetType());
        if ((RequireAutoRegister() && AutoRegAtr == null) || (AutoRegAtr != null && AutoRegAtr.RegisterType == MDAutoRegisterType.None))
        {
            return;
        }

        RegisterDebug = AutoRegAtr.RegisterType == MDAutoRegisterType.Debug;

        Instance.PopulateBindNodes();
        Instance.RegisterReplicatedAttributes();
        if (RegisterDebug)
        {
            Instance.RegisterCommandAttributes();
        }
    }

    // Unregisters a removed node from MDFramework systems
    private void UnregisterNode(Node Instance)
    {
        // We automatically unregister commands even though we don't automatically register them to avoid relying on the user to do so
        Instance.UnregisterCommandAttributes();
        Instance.UnregisterReplicatedAttributes();
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

    // Override to change when the console is available
    public virtual bool IsConsoleAvailable()
    {
        #if DEBUG
        return true;
        #else
        return false;
        #endif
    }

    // Override to change when UPNP is used for the server
    public virtual bool UseUPNP()
    {
        return true;
    }

    // Override to change is MDAutoRegister is required
    public virtual bool RequireAutoRegister()
    {
        return false;
    }

    public MDGameSession GameSession {get; private set;}
    public MDInterfaceManager InterfaceManager {get; private set;}
}
