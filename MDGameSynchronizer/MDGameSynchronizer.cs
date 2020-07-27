using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    public interface IMDSynchronizedNode
    {
        bool IsSynchronizationComplete();
    }

    ///<summary>A synchronization class with three primary features
    ///<para>It tracks the ping to each client</para><para></para>
    ///<para>It tracks what the local OS.GetTicksMsec() is on each connected client relative to the server (server only)</para><para></para>
    ///<para>Finally it takes care of making sure all players are fully synchronized on request. 
    /// Usually when a player joins or level is changed</para>
    ///</summary>
    [MDAutoRegister]
    public class MDGameSynchronizer : Node
    {
        public enum SynchronizationStates
        {
            SYNCHRONIZING_IN_PROGRESS,
            SYNCRHONIZED
        }
        public const string METHOD_REQUEST_TICKS_MSEC = nameof(RequestTicksMsec);
        public const string METHOD_ON_PING_TIMER_TIMEOUT = nameof(OnPingTimerTimeout);
        public const string METHOD_REQUEST_PING = nameof(RequestPing);
        
        private const string LOG_CAT = "LogGameSynchronizer";

        private const string RESUME_TIMER_NAME = "ResumeTimer";

        private const string ALL_PLAYERS_SYNCHED_TIMER_NAME = "AllPlayersSynchedCheck";

        private const float SYNCH_TIMER_CHECK_INTERVAL = 0.5f;

        public delegate void SynchStartedHandler(bool IsPaused);

        public event SynchStartedHandler OnSynchStartedEvent = delegate { };

        public delegate void SynchCompleteHandler(float ResumeGameIn);

        public event SynchCompleteHandler OnSynchCompleteEvent = delegate { };

        public delegate void SynchStatusUpdateHandler(int PeerId, float ProgressPercentage);

        public event SynchStatusUpdateHandler OnPlayerSynchStatusUpdateEvent = delegate { };

        public delegate void SynchPlayerPingUpdatedHandler(int PeerId, int Ping);

        public event SynchPlayerPingUpdatedHandler OnPlayerPingUpdatedEvent = delegate { };

        public MDGameInstance GameInstance = null;

        public MDGameClock GameClock = null;

        public MDGameSession GameSession = null;

        ///<Summary>List containing synch information per peer</summary>
        protected Dictionary<int, MDGameSynchPeerInfo> PeerSynchInfo = new Dictionary<int, MDGameSynchPeerInfo>();

        protected List<Node> NodeList = new List<Node>();

        protected int NodeCount = -1;

        protected bool NodeSynchCompleted = false;
        
        protected int MaxPing;

        public SynchronizationStates SynchronizationState { get; private set;}


        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            GameSession = GameInstance.GetGameSession();
            GameSession.OnPlayerJoinedEvent += OnPlayerJoinedEvent;
            GameSession.OnPlayerInitializedEvent += OnPlayerInitializedEvent;
            GameSession.OnPlayerLeftEvent += OnPlayerLeftEvent;
            GameSession.OnNetworkNodeAdded += OnNetworkNodeAdded;
            GameSession.OnNetworkNodeRemoved += OnNetworkNodeRemoved;
            GameSession.OnSessionStartedEvent += OnSessionStartedEvent;
            SynchronizationState = SynchronizationStates.SYNCHRONIZING_IN_PROGRESS;
        }

        #region PUBLIC METHODS

        /// <summary> Returns the ping for the given peer or -1 if the peer does not exist.</summary>
        public int GetPlayerPing(int PeerId)
        {
            if (PeerSynchInfo.ContainsKey(PeerId))
            {
                return PeerSynchInfo[PeerId].Ping;
            }

            MDLog.Warn(LOG_CAT, $"Requested ping for peer [{PeerId}] that doesn't exist in list");
            return -1;
        }

        ///<summary>Returns the highest ping we got to any player</summary>
        public int GetMaxPlayerPing()
        {
            return MDStatics.IsClient() ? MaxPing : PeerSynchInfo.Select(p => p.Value.Ping).Max();
        }

        /// <summary> Returns the estimated OS.GetTicksMsec for the given peer or 0 if the peer does not exist.</summary>
        public uint GetPlayerTicksMsec(int PeerId)
        {
            if (PeerSynchInfo.ContainsKey(PeerId))
            {
                return Convert.ToUInt32(OS.GetTicksMsec() + PeerSynchInfo[PeerId].TickMSecOffset);
            }

            MDLog.Warn(LOG_CAT, $"Requested OS.GetTicksMsec for peer [{PeerId}] that doesn't exist in list");
            return 0;
        }

        /// <summary>
        /// Checks if the peer has completed node synching
        /// </summary>
        /// <param name="PeerId">The peer id</param>
        /// <returns>True if they have, false if not</returns>
        public bool HasPeerCompletedNodeSynch(int PeerId)
        {
            if (!PeerSynchInfo.ContainsKey(PeerId))
            {
                return false;
            }
            return PeerSynchInfo[PeerId].CompletedNodeSynch;
        }

        #endregion

        #region RPC METHODS

        [Puppet]
        private void RpcReceiveNodeCount(int NodeCount)
        {
            MDLog.Debug(LOG_CAT, $"Total nodes that need synch are {NodeCount}");
            this.NodeCount = NodeCount;
            NodeSynchCompleted = false;
            // Start synch timer
            Timer timer = this.CreateUnpausableTimer("SynchTimer", false, SYNCH_TIMER_CHECK_INTERVAL, true, this,
                nameof(CheckSynchStatus));
            timer.Start();

            // Send out synch started event
            OnSynchStartedEvent(IsPauseOnJoin());
            // Set all peers to 0% except server
            foreach (int peerid in GameSession.GetAllPeerIds().Where(peerid => peerid != MDStatics.GetServerId()))
            {
                OnPlayerSynchStatusUpdateEvent(peerid, 0f);
            }
        }

        private void UpdateSynchStatusOnAllClients(int PeerId, float SynchStatus)
        {
            foreach (int peerid in GameSession.GetAllPeerIds())
            {
                if (peerid != MDStatics.GetServerId() && peerid != PeerId)
                {
                    RpcId(peerid, nameof(UpdateSynchStatus), PeerId, SynchStatus);
                }
            }
        }

        [Puppet]
        private void UpdateSynchStatus(int PeerId, float SynchStatus)
        {
            OnPlayerSynchStatusUpdateEvent(PeerId, SynchStatus);
        }

        [Master]
        private void ClientSynchDone()
        {
            // Great this client is done
            MDLog.Debug(LOG_CAT, $"Peer [{Multiplayer.GetRpcSenderId()}] completed synch");
            if (PeerSynchInfo.ContainsKey(Multiplayer.GetRpcSenderId()))
            {
                PeerSynchInfo[Multiplayer.GetRpcSenderId()].CompletedSynch = true;
            }

            // Send status update to all players so they can update UI
            UpdateSynchStatusOnAllClients(Multiplayer.GetRpcSenderId(), 1f);

            // Update our own UI
            OnPlayerSynchStatusUpdateEvent(Multiplayer.GetRpcSenderId(), 1f);
        }

        [Master]
        private void ClientSynchStatus(int SynchedNodes)
        {
            // Synch in progress
            MDLog.Debug(LOG_CAT,
                $"Peer [{Multiplayer.GetRpcSenderId()}] has synched {SynchedNodes} out of {NodeList.Count} nodes");

            // Send status update to all players so they can update UI
            UpdateSynchStatusOnAllClients(Multiplayer.GetRpcSenderId(), (float) SynchedNodes / NodeList.Count);

            // Update our own UI
            OnPlayerSynchStatusUpdateEvent(Multiplayer.GetRpcSenderId(), (float) SynchedNodes / NodeList.Count);
        }

        [Remote]
        private void RequestPingAndUpdateClock(uint ServerTimeOfRequest)
        {
            // Respond
            RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
            MDLog.Trace(LOG_CAT, "Responded to server request for ping");
        }

        ///<Summary>Sent by server when requesting ping, also keeping game clock in sync</summary>
        [Puppet]
        private void RequestPing(uint ServerTimeOfRequest, uint EstimateTime, uint EstimatedTick, int MaxPing)
        {
            // Set max player's ping received from server
            OnPlayerPingUpdatedEvent(MDStatics.GetServerId(), MaxPing);
            this.MaxPing = MaxPing;
            // Respond
            RequestPing(ServerTimeOfRequest);
            GameClock?.CheckSynch(EstimateTime, EstimatedTick);
        }

        ///<Summary>Sent by server to request ping or when gameclock is inactive</summary>
        [Remote]
        private void RequestPing(uint ServerTimeOfRequest)
        {
            // Respond
            RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
            MDLog.Trace(LOG_CAT, "Responded to server request for ping");
        }

        [Remote]
        private void PingResponse(uint ServerTimeOfRequest)
        {
            int ping = (int) (OS.GetTicksMsec() - ServerTimeOfRequest);
            PeerSynchInfo[Multiplayer.GetRpcSenderId()].PushPlayerPingToQueue(ping);
        }

        ///<summary>Requests the OS.GetTicksMsec() from the client</summary>
        [Puppet]
        private void RequestTicksMsec(int RequestNumber, uint ServerTimeOfRequest)
        {
            // Respond
            RpcId(GameSession.GetNetworkMaster(), nameof(ResponseTicksMsec), OS.GetTicksMsec(), ServerTimeOfRequest,
                RequestNumber);
            MDLog.Trace(LOG_CAT,
                $"Responded to server request number {RequestNumber} for OS.GetTicksMsec with [{OS.GetTicksMsec()}]");
        }

        ///<summary>Response to our OS.GetTicksMsec() request from the client</summary>
        [Master]
        private void ResponseTicksMsec(uint ClientTicksMsec, uint ServerTimeOfRequest, int RequestNumber)
        {
            MDLog.Debug(LOG_CAT,
                $"Msec response number {RequestNumber} from peer [{Multiplayer.GetRpcSenderId()}] is {ClientTicksMsec} local Msec is {OS.GetTicksMsec()}");
            PeerSynchInfo[Multiplayer.GetRpcSenderId()].ProcessMSecResponse(ClientTicksMsec, ServerTimeOfRequest, RequestNumber);
        }

        [Puppet]
        private void PauseGame()
        {
            if (!IsPauseOnJoin())
            {
                return;
            }

            GetTree().Paused = true;

            // Check if resume timer is active, if so kill it
            Timer resumeTimer = (Timer) GetNodeOrNull(RESUME_TIMER_NAME);
            if (resumeTimer != null)
            {
                resumeTimer.Stop();
                resumeTimer.RemoveAndFree();
            }
            else
            {
                // Only clear the list if synching isn't already in progress
                foreach (MDGameSynchPeerInfo peerInfo in PeerSynchInfo.Values)
                {
                    peerInfo.CompletedSynch = false;
                }
            }
        }

        [PuppetSync]
        private void UnpauseAtTickMsec(uint UnpauseTime, uint GameTickToUnpauseAt)
        {
            float waitTime = (UnpauseTime - OS.GetTicksMsec()) / 1000f;
            MDLog.Trace(LOG_CAT, $"Unpausing game in {waitTime}");
            Timer timer = this.CreateUnpausableTimer(RESUME_TIMER_NAME, true, waitTime, true, this,
                nameof(OnUnpauseTimerTimeout));
            timer.Start();
            OnSynchCompleteEvent(waitTime);
            if (GameClock != null && MDStatics.IsClient())
            {
                GameClock.SetCurrentTick(GameTickToUnpauseAt);
            }
        }

        [Master]
        private void NotifyAllNodesSynched()
        {
            // Called from client the first time they complete their node synch
            MDLog.Trace(LOG_CAT, $"Peer [{Multiplayer.GetRpcSenderId()}] has completed node synchronization");
            if (!PeerSynchInfo.ContainsKey(Multiplayer.GetRpcSenderId()))
            {
                PeerSynchInfo[Multiplayer.GetRpcSenderId()].CompletedNodeSynch = true;
            }
        }

        #endregion

        #region SUPPORTING METHODS

        /// <summary>
        /// Send player ping event
        /// </summary>
        /// <param name="PeerId">The peer to set ping for</param>
        /// <param name="Ping">The ping</param>
        public void SendPlayerPingEvent(int PeerId, int Ping)
        {
            OnPlayerPingUpdatedEvent(PeerId, Ping);
        }

        private void OnPlayerJoinedEvent(int PeerId)
        {
            // Check if this is our own join message or if we are a client
            if (PeerId == MDStatics.GetPeerId() || MDStatics.IsClient())
            {
                return;
            }
            
            MDGameSynchPeerInfo PeerInfo = new MDGameSynchPeerInfo(this, PeerId);
            PeerSynchInfo.Add(PeerId, PeerInfo);

            if (IsPauseOnJoin())
            {
                PauseGame();
                foreach (int peerid in GameSession.GetAllPeerIds().Where(peerid => peerid != MDStatics.GetServerId()))
                {
                    // Don't do this for the connecting peer or the server
                    if (PeerId != peerid)
                    {
                        RpcId(peerid, nameof(PauseGame));
                    }
                }
            }
        }

        private void OnPlayerInitializedEvent(int PeerId)
        {
            // Check if this is our own join message or if we are a client
            if (PeerId == MDStatics.GetPeerId() || MDStatics.IsClient())
            {
                return;
            }

            OnSynchStartedEvent(IsPauseOnJoin());
            foreach (int peerid in GameSession.GetAllPeerIds().Where(peerid => peerid != MDStatics.GetServerId()))
            {
                // Synch just started so set everyone to 0%
                OnPlayerSynchStatusUpdateEvent(peerid, 0f);
                RpcId(peerid, nameof(RpcReceiveNodeCount), NodeList.Count);
            }

            // Start synch check timer
            Timer timer = (Timer) GetNodeOrNull(ALL_PLAYERS_SYNCHED_TIMER_NAME);
            if (timer == null)
            {
                timer = this.CreateUnpausableTimer(ALL_PLAYERS_SYNCHED_TIMER_NAME, false, SYNCH_TIMER_CHECK_INTERVAL, true,
                    this, nameof(CheckAllClientsSynched));
                timer.Start();
            }
        }

        private void OnPlayerLeftEvent(int PeerId)
        {
            if (PeerSynchInfo.ContainsKey(PeerId))
            {
                PeerSynchInfo[PeerId].Dispose();
                PeerSynchInfo.Remove(PeerId);
            }
        }

        private void OnSessionStartedEvent()
        {
            if (this.IsClient())
            {
                // Add max ping information between this client and any other client
                // Roundtrip is: client 1 -> server -> client 2 -> server -> client 1.
                // TODO: Max ping should be not identical for each player we ping
                MDOnScreenDebug.AddOnScreenDebugInfo($"MaxRoundtripPing: ",
                    () => MaxPing.ToString());
                PauseGame();
                SynchronizationState = SynchronizationStates.SYNCHRONIZING_IN_PROGRESS;
            }
            else
            {
                SynchronizationState = SynchronizationStates.SYNCRHONIZED;
            }

            // Reset to tick 0 at start of session
            GameClock?.SetCurrentTick(0);
        }

        private void OnPingTimerTimeout(int PeerId)
        {
            if (PeerSynchInfo.ContainsKey(PeerId))
            {
                PeerSynchInfo[PeerId].OnPingTimerTimeout();
            }
        }

        private void CheckAllClientsSynched(Timer timer)
        {
            if (PeerSynchInfo.Values.Where(peerInfo => peerInfo.CompletedSynch == false).ToList().Count > 0)
            {
                MDLog.Debug(LOG_CAT, "All clients are not synched yet");
                return;
            }

            // Check if we still need to wait for a better confidence on the TickMsec value
            if (PeerSynchInfo.Values.Where(peerInfo => peerInfo.IsClientMSecConfident() == false).ToList().Count > 0)
            {
                MDLog.Debug(LOG_CAT, "Still waiting for a more secure msec value");
                return;
            }

            MDLog.Debug(LOG_CAT, "All clients synched, sending unpause signal");
            // Alright tell all clients to unpause in a bit
            foreach (int peerid in GameSession.GetAllPeerIds())
            {
                // Get our current game tick
                uint tickToUnpause = GameClock?.GetTick() != null ? GameClock.GetTick() : 0;

                if (peerid != MDStatics.GetServerId())
                {
                    RpcId(peerid, nameof(UnpauseAtTickMsec),
                        GetPlayerTicksMsec(peerid) + GetUnpauseCountdownDurationMSec(), tickToUnpause);
                }
            }

            UnpauseAtTickMsec(OS.GetTicksMsec() + GetUnpauseCountdownDurationMSec(), 0);
            PeerSynchInfo.Values.ToList().ForEach(value => value.CompletedSynch = false);
            timer.RemoveAndFree();
        }

        private void OnNetworkNodeAdded(Node node)
        {
            MDLog.Trace(LOG_CAT, $"Node removed: {node.GetPath()}");
            NodeList.Add(node);
        }

        private void OnNetworkNodeRemoved(Node node)
        {
            MDLog.Trace(LOG_CAT, $"Node removed: {node.GetPath()}");
            NodeList.Remove(node);
        }

        private void CheckSynchStatus(Timer timer)
        {
            // Only do this on clients
            if (!this.IsClient())
            {
                timer.RemoveAndFree();
                return;
            }

            if (NodeCount < 0)
            {
                MDLog.Debug(LOG_CAT, "We got no node count");
                // We don't know how many nodes we got yet
                return;
            }

            int SynchedNodes = NodeCount;
            if (NodeList.Count < NodeCount)
            {
                MDLog.Debug(LOG_CAT, "We still don't have all nodes");
                // We still don't have all nodes
                SynchedNodes = NodeList.Count;
            }
            else if (!NodeSynchCompleted)
            {
                // This is the first time we synched all nodes, notify the server
                MDLog.Debug(LOG_CAT, "Node synch complete, notifying server");
                NodeSynchCompleted = true;
                RpcId(GameSession.GetNetworkMaster(), nameof(NotifyAllNodesSynched));
            }

            int NotSynchedNodes = 0;

            // Check node custom logic to see if synch is done
            foreach (Node node in NodeList)
            {
                if (!(node is IMDSynchronizedNode) || ((IMDSynchronizedNode) node).IsSynchronizationComplete())
                {
                    continue;
                }

                // We are not synched
                MDLog.Trace(LOG_CAT, $"A node is still synching: {node.GetPath()}");
                SynchedNodes--;
                NotSynchedNodes++;
            }

            if (SynchedNodes == NodeCount)
            {
                // We are done synching
                SynchronizationState = SynchronizationStates.SYNCRHONIZED;
                RpcId(MDStatics.GetServerId(), nameof(ClientSynchDone));
                MDLog.Debug(LOG_CAT, "We are done synching notifying server");
                // Set ourselves to done
                OnPlayerSynchStatusUpdateEvent(MDStatics.GetPeerId(), 1f);
                timer.RemoveAndFree();
            }
            else
            {
                float percentage = (float) SynchedNodes / NodeCount;
                MDLog.Debug(LOG_CAT,
                    $"We have {NotSynchedNodes} nodes that are still synching. Current status: {percentage * 100}%");
                // Notify the server of how many nodes we got synched
                RpcId(GameSession.GetNetworkMaster(), nameof(ClientSynchStatus), SynchedNodes);

                // Update our own UI
                OnPlayerSynchStatusUpdateEvent(MDStatics.GetPeerId(), percentage);
            }
        }

        private void OnUnpauseTimerTimeout(Timer timer)
        {
            MDLog.Trace(LOG_CAT, "Unpausing game");
            timer.RemoveAndFree();
            GetTree().Paused = false;
        }

        #endregion

        #region VIRTUAL METHODS

        /// <summary>Pauses the game for synching on player join (Default: True)</summary>
        protected virtual bool IsPauseOnJoin()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.PAUSE_ON_JOIN, true);
        }

        /// <summary>Delay MDReplicator until all nodes are synched (Default: False)</summary>
        public virtual bool IsDelayReplicatorUntilAllNodesAreSynched()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.DELAY_REPLICATION_UNTIL_ALL_NODES_SYNCHED, false);
        }

        /// <summary>Unpause countdown duration (Default: 2 seconds)</summary>
        protected virtual uint GetUnpauseCountdownDurationMSec()
        {
            return (uint) this.GetConfiguration().GetInt(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.UNPAUSE_COUNTDOWN_DURATION, 2000);
        }

        /// <summary>How often do we ping each client (Default: 0.5f)</summary>
        public virtual float GetPingInterval()
        {
            return float.Parse(this.GetConfiguration().GetString(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.PING_INTERVAL, "0.5"));
        }

        /// <summary>Pings to keep for getting average (Default: 10)</summary>
        public virtual int GetPingsToKeepForAverage()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.PINGS_TO_KEEP_FOR_AVERAGE, 10);
        }

        /// <summary>If set to true we will ping every player continuously. (Default: true)
        /// <para>You can set the interval with <see cref="GetPingInterval"/></para></summary>
        public virtual bool IsActivePingEnabled()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.ACTIVE_PING_ENABLED, true);
        }

        /// <summary>This decides how many times we go back and forth to establish the OS.GetTicksMsec offset for each client (Default: 20)</summary>
        public virtual int GetInitialMeasurementCount()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.INITIAL_MEASUREMENT_COUNT, 20);
        }

        /// <summary>If IsPauseOnJoin() is enabled we will wait for at least this level of security for TicksMsec before we resume (Default: GetInitialMeasurementCount() / 2)</summary>
        public virtual int GetMinimumMeasurementCountBeforeResume()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfigurationSections.GameSynchronizer, MDConfiguration.INITIAL_MEASUREMENT_COUNT_BEFORE_RESUME, 10);
        }

        #endregion
    }
}