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
        private string _playerName = "";

        /// <summary>
        /// Initializes the player info
        /// </summary>
        /// <param name="PlayerPeerId">The peer id this player info is for</param>
        public void InitPlayerInfo(int PlayerPeerId)
        {
            PeerId = PlayerPeerId;
            Name = PeerId.ToString();
            SetNetworkMaster(PeerId);
            if (PeerId == MDStatics.GetPeerId())
            {
                SetPlayerName(GetPlayerNameInt());
            }
            else
            {
                // Set a default name for other peers
                _playerName = "Peer" + PeerId;
            }
        }

        /// <summary>
        /// Override this to set the player name when the player info is initialized
        /// </summary>
        /// <returns>The player name</returns>
        protected virtual string GetPlayerNameInt()
        {
            return "Player" + PeerId;
        }

        /// <summary>
        /// Sets the player name
        /// </summary>
        /// <param name="name">The name of the player</param>
        public void SetPlayerName(string name)
        {
            SetPlayerNameInt(name);
        }

        /// <summary>
        /// Returns the name of this player
        /// </summary>
        /// <returns>The name of the player</returns>
        public string GetPlayerName()
        {
            return _playerName;
        }

        /// <summary>
        /// Called when a new player joins, used to synchronize the new player
        /// <para>PeerId is the ID of the new player that joined</para>
        /// </summary>
        /// <param name="PeerId">The ID of the new player that joined</param>
        public virtual void PerformFullSync(int PeerId)
        {
            // Sync the new peer
            RpcId(PeerId, nameof(SetPlayerNameInt), GetPlayerName());
        }

        /// <summary>
        /// Sends the player name to other clients
        /// </summary>
        /// <param name="name">The player name</param>
        [Puppet]
        protected void SetPlayerNameInt(string name)
        {
            _playerName = name;
            MDStatics.GetGameSession().NotifyPlayerNameChanged(PeerId);
            if (PeerId == MDStatics.GetPeerId() && MDStatics.IsNetworkActive())
            {
                // Notify other clients
                Rpc(nameof(SetPlayerNameInt), name);
            }
        }

        /// <summary>
        /// The network ID of the peer this PlayerInfo belongs to
        /// </summary>
        /// <value>A network ID</value>
        public int PeerId { get; private set; }
    }
}