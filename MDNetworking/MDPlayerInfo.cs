using Godot;
using System;

namespace MD
{
    /// <summary>
    /// Class that tracks the players game data that should be replicated to other players
    /// </summary>
    [MDAutoRegister]
    public class MDPlayerInfo : Node
    {
        private const string LOG_CAT = "LogPlayerInfo";
        [MDReplicated()]
        [MDReplicatedSetting(MDReplicatedMember.Settings.OnValueChangedEvent, nameof(OnPlayerNameChanged))]
        public string PlayerName { get; protected set; } = "";

        public event Action<string> OnPlayerNameChangedEvent = delegate { };

        /// <summary>
        /// The network ID of the peer this PlayerInfo belongs to
        /// </summary>
        /// <value>A network ID</value>
        public int PeerId { get; private set; }

        /// <summary>
        /// Whether or not this player info has completed initialization
        /// </summary>
        public bool HasInitialized { get; internal set; } = false;

        public override void _Ready()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        }

        public override void _Process(float delta)
        {
            if (MDStatics.IsClient() && PlayerName != "" && PeerId == 1)
            {
                MDLog.Force(LOG_CAT, PlayerName);
            }
        }

        /// <summary>
        /// Sets the node Name and stores the PeerId for the PlayerInfo
        /// </summary>
        /// <param name="PlayerPeerId">The peer id this player info is for</param>
        internal void SetPeerId(int PlayerPeerId)
        {
            PeerId = PlayerPeerId;
            Name = PeerId.ToString();
        }
        
        internal void BeginInitialization()
        {
            MDLog.Debug(LOG_CAT, $"Starting initialization for PeerId [{PeerId}]");
            if (PeerId == MDStatics.GetPeerId())
            {
                OnServerRequestedInitialization();
            }
            else
            {
                // Send message to owning client from server to request initialization data
                this.MDRpcId(PeerId, nameof(OnServerRequestedInitialization));
            }
        }

        [Remote]
        protected virtual void OnServerRequestedInitialization()
        {
            MDLog.Debug(LOG_CAT, $"Initializing PeerId [{PeerId}] from owner");
            string PlaceholderName = "Player_" + PeerId;
            if (MDStatics.IsServer())
            {
                OnClientSentPlayerName(PlaceholderName);
            }
            else
            {
                this.MDServerRpc(nameof(OnClientSentPlayerName), PlaceholderName);
            }
        }

        [Remote]
        protected virtual void OnClientSentPlayerName(string ClientName)
        {
            MDLog.Debug(LOG_CAT, $"Server received initialization for PeerId [{PeerId}] from owner");
            if (HasInitialized == false)
            {
                PlayerName = ClientName;
                this.GetGameSession().OnPlayerInfoInitializationCompleted(PeerId);
            }
        }

        private void OnPlayerNameChanged()
        {
            OnPlayerNameChangedEvent(PlayerName);
        }
    }
}