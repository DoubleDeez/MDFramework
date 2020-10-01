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
        [MDReplicated]
        [MDReplicatedSetting(MDReplicatedMember.Settings.OnValueChangedEvent, nameof(OnPlayerNameChangedEvent))]
        [MDReplicatedSetting(MDReplicatedMember.Settings.CallOnValueChangedEventLocally, true)]
        public string PlayerName { get; protected set; } = "";

        /// <summary>
        /// When the player name changes, this event will fire with the new name as the parameter
        /// </summary>
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
                RpcId(PeerId, nameof(OnServerRequestedInitialization));
            }
        }

        /// <summary>
        /// Called on the corresponding client for this player info to begin initialization
        /// </summary>
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
                RpcId(MDStatics.GetServerId(), nameof(OnClientSentPlayerName), PlaceholderName);
            }
        }

        /// <summary>
        /// As part of initialization, this is call on the server from the client to initialize the player info with the name
        /// </summary>
        /// <param name="ClientName">The player name the client wants</param>
        [Remote]
        protected virtual void OnClientSentPlayerName(string ClientName)
        {
            MDLog.Debug(LOG_CAT, $"Server received initialization for PeerId [{PeerId}] from owner");
            if (HasInitialized == false)
            {
                PlayerName = ClientName;
                MarkPlayerInitializationCompleted();
            }
        }

        /// <summary>
        /// Notifies the server that initialization for this player has completed
        /// </summary>
        protected void MarkPlayerInitializationCompleted()
        {
            if (MDStatics.IsServer())
            {
                ServerMarkPlayerInitializationCompleted();
            }
            else
            {
                RpcId(MDStatics.GetServerId(), nameof(ServerMarkPlayerInitializationCompleted));
            }
        }

        [Remote]
        private void ServerMarkPlayerInitializationCompleted()
        {   
            if (HasInitialized == false)
            {
                this.GetGameSession().OnPlayerInfoInitializationCompleted(PeerId);
            }
        }
    }
}