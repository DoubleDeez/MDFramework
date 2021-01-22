using Godot;
using System;

namespace MD
{
    /// <summary>
    /// Single-instance class that persists throughout the life-time of the game application.
    /// </summary>
    public class MDGameInstance : Node
    {
        private const string LOG_CAT = "LogGameInstance";
        public MDReplicator Replicator { get; private set; }
        public MDConfiguration Configuration { get; private set; }

        public MDGameSession GameSession { get; private set; }
        public MDGameClock GameClock { get; private set; }
        public MDGameSynchronizer GameSynchronizer { get; private set; }
        public MDInterfaceManager InterfaceManager { get; private set; }

        // TODO - There should be an InputState for each local player
        public MDInput InputState { get; protected set; } = new MDInput();

        public override void _Ready()
        {
            MDStatics.GI = this;
            
            // Configuration first
            CreateConfiguration();

            // Init static classes first
            MDLog.Initialize(GetLogDirectory());
            MDArguments.PopulateArgs();
            MDProfiler.Initialize();
            MDOnScreenDebug.Initialize();

            // Hook up events
            GetTree().Connect("node_added", this, nameof(OnNodeAdded_Internal));
            GetTree().Connect("node_removed", this, nameof(OnNodeRemoved_Internal));

            // Init instances
            CreateGameSession();
            CreateGameSynchronizer();
            CreateReplicator();
            CreateInterfaceManager();

            RegisterNodeAndChildren(GetTree().Root);
        }

        public override void _Notification(int NotificationType)
        {
            base._Notification(NotificationType);

            switch (NotificationType)
            {
                case MainLoop.NotificationWmQuitRequest:
                    MDLog.Info(LOG_CAT, "Quit notification received.");
                    GameSession?.Disconnect();
                    break;
            }
        }

        public override void _Input(InputEvent Event)
        {
            InputState.OnInputEvent(Event);
        }

        /// <summary>
        /// Called whenever a new node is added to the scene
        /// </summary>
        /// <param name="AddedNode">The node that was added</param>
        protected virtual void OnNodeAdded(Node AddedNode)
        {
        }

        /// <summary>
        /// Called whenever a node is removed to the scene
        /// </summary>
        /// <param name="RemovedNode">The node that was removed</param>
        protected virtual void OnNodeRemoved(Node RemovedNode)
        {
        }

        // Travels the tree and registers the existing nodes
        private void RegisterNodeAndChildren(Node RootNode)
        {
            if (RootNode == null)
            {
                return;
            }

            OnNodeAdded_Internal(RootNode);
            int ChildCount = RootNode.GetChildCount();
            for (int i = 0; i < ChildCount; ++i)
            {
                RegisterNodeAndChildren(RootNode.GetChild(i));
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
            if (RemovedNode == null)
            {
                return;
            }

            UnregisterNode(RemovedNode);
            OnNodeRemoved(RemovedNode);
            if (GameSession != null && GameSession != RemovedNode)
            {
                GameSession.OnNodeRemoved(RemovedNode);
            }
        }

        // Registers a new node to MDFramework systems
        private void RegisterNewNode(Node Instance)
        {
            Type type = Instance.GetType();

            // Ignore nodes in Godot namespace as they won't have any attributes
            if (MDStatics.IsInGodotNamespace(type))
            {
                return;
            }

            MDAutoRegister AutoRegAtr = MDStatics.FindClassAttributeInNode<MDAutoRegister>(Instance.GetType());
            if (RequireAutoRegister() && AutoRegAtr == null ||
                AutoRegAtr != null && AutoRegAtr.RegisterType == MDAutoRegisterType.None)
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

        // Ensure Replicator is created
        private void CreateReplicator()
        {
            if (Replicator == null)
            {
                Replicator = MDStatics.CreateTypeInstance<MDReplicator>(GetReplicatorType());
                Replicator.Name = "Replicator";
                this.AddNodeToRoot(Replicator, true);
                Replicator.Initialize();
                GameSession.Replicator = Replicator;
            }
        }

        // Ensure Replicator is created
        private void CreateConfiguration()
        {
            if (Configuration == null)
            {
                Configuration = MDStatics.CreateTypeInstance<MDConfiguration>(GetConfigurationType());
                Configuration.Name = "MDConfiguration";
                Configuration.LoadConfiguration();
                this.AddNodeToRoot(Configuration, true);
            }
        }

        // Ensure GameSession is created
        private void CreateGameSession()
        {
            if (GameSession == null)
            {
                GameSession = MDStatics.CreateTypeInstance<MDGameSession>(GetGameSessionType());
                GameSession.Name = "GameSession";
                GameSession.GameInstance = this;
                this.AddNodeToRoot(GameSession, true);
            }
        }

        private void CreateGameSynchronizer()
        {
            if (GameSynchronizer == null)
            {
                GameSynchronizer = MDStatics.CreateTypeInstance<MDGameSynchronizer>(GetGameSynchronizerType());
                GameSynchronizer.Name = "GameSynchronizer";
                GameSynchronizer.GameInstance = this;
                this.AddNodeToRoot(GameSynchronizer, true);

                // Check if we should create the game clock as well
                CreateGameClock();
            }
        }

        private void CreateGameClock()
        {
            if (GameClock == null)
            {
                GameClock = MDStatics.CreateTypeInstance<MDGameClock>(GetGameClockType());
                GameClock.Name = "GameClock";
                GameSynchronizer.GameClock = GameClock;
                this.AddNodeToRoot(GameClock, true);
            }
        }

        // Ensure InterfaceManager is created
        private void CreateInterfaceManager()
        {
            if (InterfaceManager == null)
            {
                InterfaceManager = new MDInterfaceManager
                {
                    Name = "InterfaceManager"
                };

                this.AddNodeToRoot(InterfaceManager, true);
            }
        }

        #region Configuration

        /// <summary>Override this to provide the your GameSession subclass type</summary>
        protected virtual Type GetGameSessionType()
        {
            return Configuration.GetType(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.GAME_SESSION_TYPE, typeof(MDGameSession));
        }

        /// <summary>Override this to provide the your GameSynchronizer subclass type</summary>
        protected virtual Type GetGameSynchronizerType()
        {
            return Configuration.GetType(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.GAME_SYNCHRONIZER_TYPE, typeof(MDGameSynchronizer));
        }

        /// <summary>Override this to provide the your GameClock subclass type</summary>
        protected virtual Type GetGameClockType()
        {
            return Configuration.GetType(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.GAME_CLOCK_TYPE, typeof(MDGameClock));
        }

        /// <summary>Override this to provide your Replicator subclass type</summary>
        protected virtual Type GetReplicatorType()
        {
            return Configuration.GetType(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.REPLICATOR_TYPE, typeof(MDReplicator));
        }

        /// <summary>Override this to provide your Configuration subclass type</summary>
        protected virtual Type GetConfigurationType()
        {
            return typeof(MDConfiguration);
        }

        // Override this to provide your own Player class type
        public virtual Type GetPlayerInfoType()
        {
            return Configuration.GetType(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.PLAYER_INFO_TYPE, typeof(MDPlayerInfo));
        }

        /// <summary>Override to change when the console is available (Default: Only in debug mode)</summary>
        public virtual bool IsConsoleAvailable()
        {
            return Configuration.GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.CONSOLE_ENABLED, false);
        }

        /// <summary>Override to change when the on screen debug is available (Default: Only in debug mode)</summary>
        public virtual bool IsOnScreenDebugAvailable()
        {
            return Configuration.GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.ON_SCREEN_DEBUG_ENABLED, false);
        }

        /// <summary>Override to change when UPNP is used for the server (Default: True)</summary>
        public virtual bool UseUPNP()
        {
            return Configuration.GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.USE_UPNP, false);
        }

        /// <summary>Override to change is MDAutoRegister is required (Default: False)</summary>
        public virtual bool RequireAutoRegister()
        {
            return Configuration.GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.REQUIRE_AUTO_REGISTER, false);
        }

        ///<summary>Get the key used to open the console. (Default: KeyList.QuoteLeft)</summary>
        public virtual int GetConsoleKey()
        {
            return Configuration.GetInt(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.CONSOLE_KEY, (int) KeyList.Quoteleft);
        }

        ///<summary>Get the key used to open the on screen debug. (Default: KeyList.F12)</summary>
        public virtual int GetOnScreenDebugKey()
        {
            return Configuration.GetInt(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.ON_SCREEN_DEBUG_KEY, (int) KeyList.F12);
        }

        ///<summary>Get the directory for MDLog log files
        ///<para>Official documentation for the user path: https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html</para></summary>
        public virtual string GetLogDirectory()
        {
            return Configuration.GetString(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.LOG_DIRECTORY, "user://logs/");
        }

        ///<summary>If true we will keep a reference to all loaded scenes around so we don't need to load the resource from disc every time</summary>
        public virtual bool UseSceneBuffer(string NodePath)
        {
            return Configuration.GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.USE_SCENE_BUFFER, true);
        }

        #endregion
    }
}
