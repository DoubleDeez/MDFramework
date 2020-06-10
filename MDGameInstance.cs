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
        MDLog.Initialize(GetLogDirectory());
        MDArguments.PopulateArgs();
        MDProfiler.Initialize();

        // Hook up events
        GetTree().Connect("node_added", this, nameof(OnNodeAdded_Internal));
        GetTree().Connect("node_removed", this, nameof(OnNodeRemoved_Internal));

        // Init instances
        CreateGameSession();
        CreateGameSynchronizer();
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

    public override void _Input(InputEvent Event)
    {
        InputState.OnInputEvent(Event);
    }

    /// <summary>Override this to provide the your GameSession subclass type</summary>
    protected virtual Type GetGameSessionType()
    {
        return typeof(MDGameSession);
    }

    /// <summary>Override this to provide the your GameSynchronizer subclass type</summary>
    protected virtual Type GetGameSynchronizerType()
    {
        return typeof(MDGameSynchronizer);
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
        MDAutoRegister AutoRegAtr = MDStatics.FindClassAttribute<MDAutoRegister>(Instance.GetType());
        if ((RequireAutoRegister() && AutoRegAtr == null) || (AutoRegAtr != null && AutoRegAtr.RegisterType == MDAutoRegisterType.None))
        {
            return;
        }

        Instance.PopulateBindNodes();
        Instance.RegisterReplicatedAttributes();
        if (AutoRegAtr != null && AutoRegAtr.RegisterType == MDAutoRegisterType.Debug)
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

    ///<summary>Ensure GameSession is created</summary>
    private void CreateGameSession()
    {
        if (GameSession == null)
        {
            GameSession = CreateTypeInstance<MDGameSession>(GetGameSessionType());
            GameSession.Name = "GameSession";
            GameSession.GameInstance = this;
            this.AddNodeToRoot(GameSession, true);
        }
    }

    private void CreateGameSynchronizer()
    {
        if (GameSynchronizer == null && UseGameSynchronizer())
        {
            GameSynchronizer = CreateTypeInstance<MDGameSynchronizer>(GetGameSynchronizerType());
            GameSynchronizer.Name = "GameSynchronizer";
            GameSynchronizer.GameInstance = this;
            this.AddNodeToRoot(GameSynchronizer, true);
        }
    }

    /// <summary>Creates an instance of the type based on the base class T</summary>
    private T CreateTypeInstance<T>(Type Type) where T: class
    {
        if (!MDStatics.IsSameOrSubclass(Type, typeof(T)))
        {
            MDLog.Error(LOG_CAT, "Type [{0}] is not a subclass of [{1}]", Type.Name, typeof(T).Name);
            return null;
        }
        
        return Activator.CreateInstance(Type) as T;
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

    /// <summary>Override to change when the console is available (Default: Only in debug mode)</summary>
    public virtual bool IsConsoleAvailable()
    {
        #if DEBUG
        return true;
        #else
        return false;
        #endif
    }

    /// <summary>Override to change when UPNP is used for the server (Default: True)</summary>
    public virtual bool UseUPNP()
    {
        return true;
    }

    /// <summary>Override to change is MDAutoRegister is required (Default: False)</summary>
    public virtual bool RequireAutoRegister()
    {
        return false;
    }

    ///<summary>Get the key used to open the console. (Default: KeyList.QuoteLeft)</summary>
    public virtual int GetConsoleKey()
    {
        return (int)KeyList.Quoteleft;
    }

    ///<summary>Decides if the network synchronizer is used or not (Default: True)</summary>
    public virtual bool UseGameSynchronizer()
    {
        return true;
    }

    ///<summary>Get the directory for MDLog logfiles
    ///<para>Official documentation for the user path: https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html</para></summary>
    public virtual String GetLogDirectory()
    {
        return "user://logs/";
    }

    ///<summary>If true we will keep a reference to all loaded scenes around so we don't need to load the resource from disc every time</summary>
    public virtual bool UseSceneBuffer(String NodePath)
    {
        return true;
    }

    public MDGameSession GameSession {get; private set;}

    public MDGameSynchronizer GameSynchronizer {get; private set;}
    public MDInterfaceManager InterfaceManager {get; private set;}

    // TODO - There should be an InputState for each local player
    public MDInput InputState { get; protected set; } = new MDInput();
}
