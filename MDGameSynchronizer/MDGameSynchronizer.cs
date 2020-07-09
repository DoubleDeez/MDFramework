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

        ///<Summary>Stores the latest ping response times for each player</summary>
        protected Dictionary<int, Queue<int>> InternalPingList = new Dictionary<int, Queue<int>>();

        ///<Summary>Stores all the estimated GetTicksMsec offsets for all players</summary>
        protected Dictionary<int, List<int>> InternalTicksList = new Dictionary<int, List<int>>();

        ///<Summary>This contains what we think is the GetTicksMsec offset for each player</summary>
        protected Dictionary<int, int> PlayerTicksMsecOffset = new Dictionary<int, int>();

        ///<Summary>This contains what we think is the ping for each player</summary>
        protected Dictionary<int, int> PlayerPing = new Dictionary<int, int>();

        protected List<Node> NodeList = new List<Node>();

        protected List<int> ClientSynchList = new List<int>();

        protected List<int> CompletedNodeSyncList = new List<int>();

        protected int NodeCount = -1;

        protected bool NodeSynchCompleted = false;
        protected int MaxPing;


        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            GameSession = GameInstance.GetGameSession();
            GameSession.OnPlayerJoinedEvent += OnPlayerJoinedEvent;
            GameSession.OnPlayerLeftEvent += OnPlayerLeftEvent;
            GameSession.OnNetworkNodeAdded += OnNetworkNodeAdded;
            GameSession.OnNetworkNodeRemoved += OnNetworkNodeRemoved;
            GameSession.OnSessionStartedEvent += OnSessionStartedEvent;
        }

        #region PUBLIC METHODS

        /// <summary> Returns the ping for the given peer or -1 if the peer does not exist.</summary>
        public int GetPlayerPing(int PeerId)
        {
            if (PlayerPing.ContainsKey(PeerId))
            {
                return PlayerPing[PeerId];
            }

            MDLog.Warn(LOG_CAT, $"Requested ping for peer [{PeerId}] that doesn't exist in list");
            return -1;
        }

        ///<summary>Returns the highest ping we got to any player</summary>
        public int GetMaxPlayerPing()
        {
            return MDStatics.IsClient() ? MaxPing : PlayerPing.Values.Max();
        }

        /// <summary> Returns the estimated OS.GetTicksMsec for the given peer or 0 if the peer does not exist.</summary>
        public uint GetPlayerTicksMsec(int PeerId)
        {
            if (PlayerTicksMsecOffset.ContainsKey(PeerId))
            {
                return Convert.ToUInt32(OS.GetTicksMsec() + PlayerTicksMsecOffset[PeerId]);
            }

            MDLog.Warn(LOG_CAT, $"Requested OS.GetTicksMsec for peer [{PeerId}] that doesn't exist in list");
            return 0;
        }

        public bool HasPeerCompletedNodeSynch(int PeerId)
        {
            return CompletedNodeSyncList.Contains(PeerId);
        }

        #endregion

        #region RPC METHODS

        [Puppet]
        protected void RpcReceiveNodeCount(int NodeCount)
        {
            MDLog.Debug(LOG_CAT, $"Total nodes that need synch are {NodeCount}");
            this.NodeCount = NodeCount;
            NodeSynchCompleted = false;
            // Start synch timer
            Timer timer = CreateUnpausableTimer("SynchTimer", false, SYNCH_TIMER_CHECK_INTERVAL, true, this,
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
        protected void UpdateSynchStatus(int PeerId, float SynchStatus)
        {
            OnPlayerSynchStatusUpdateEvent(PeerId, SynchStatus);
        }

        [Master]
        protected void ClientSynchDone()
        {
            // Great this client is done
            MDLog.Debug(LOG_CAT, $"Peer [{Multiplayer.GetRpcSenderId()}] completed synch");
            if (!ClientSynchList.Contains(Multiplayer.GetRpcSenderId()))
            {
                ClientSynchList.Add(Multiplayer.GetRpcSenderId());
            }

            // Send status update to all players so they can update UI
            UpdateSynchStatusOnAllClients(Multiplayer.GetRpcSenderId(), 1f);

            // Update our own UI
            OnPlayerSynchStatusUpdateEvent(Multiplayer.GetRpcSenderId(), 1f);
        }

        [Master]
        protected void ClientSynchStatus(int SynchedNodes)
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
        protected void RequestPingAndUpdateClock(uint ServerTimeOfRequest)
        {
            // Respond
            RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
            MDLog.Trace(LOG_CAT, "Responded to server request for ping");
        }

        ///<Summary>Sent by server when requesting ping, also keeping game clock in sync</summary>
        [Puppet]
        protected void RequestPing(uint ServerTimeOfRequest, uint EstimateTime, uint EstimatedTick, int MaxPing)
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
        protected void RequestPing(uint ServerTimeOfRequest)
        {
            // Respond
            RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
            MDLog.Trace(LOG_CAT, "Responded to server request for ping");
        }

        [Remote]
        protected void PingResponse(uint ServerTimeOfRequest)
        {
            int ping = (int) (OS.GetTicksMsec() - ServerTimeOfRequest);
            PushPlayerPingToQueue(Multiplayer.GetRpcSenderId(), ping);
        }

        ///<summary>Requests the OS.GetTicksMsec() from the client</summary>
        [Puppet]
        protected void RequestTicksMsec(int RequestNumber, uint ServerTimeOfRequest)
        {
            // Respond

            RpcId(GameSession.GetNetworkMaster(), nameof(ResponseTicksMsec), OS.GetTicksMsec(), ServerTimeOfRequest,
                RequestNumber);
            MDLog.Trace(LOG_CAT,
                $"Responded to server request number {RequestNumber} for OS.GetTicksMsec with [{OS.GetTicksMsec()}]");
        }

        ///<summary>Response to our OS.GetTicksMsec() request from the client</summary>
        [Master]
        protected void ResponseTicksMsec(uint ClientTicksMsec, uint ServerTimeOfRequest, int RequestNumber)
        {
            MDLog.Debug(LOG_CAT,
                "Msec response number {RequestNumber} from peer [{Multiplayer.GetRpcSenderId()}] is {ClientTicksMsec} local Msec is {OS.GetTicksMsec()}");
            // Get and record ping
            int ping = (int) (OS.GetTicksMsec() - ServerTimeOfRequest);
            PushPlayerPingToQueue(Multiplayer.GetRpcSenderId(), ping);

            // Calculate ping for one way trip (Ping / 2)
            long pingOneWay = (long) (Mathf.Floor(ping) / 2f);

            /* Estimation is <Client TickMsec> - <Server TickMsec> + <Ping Half Time> = <Estimated Current Offset To Client>
                Example:
                ClientTime when request was recieved = 3000
                ServerTime when response came = 5000
                Ping for rountrip = 500.
                    If we assume the packet took the same time back and forth it means 
                    the client is half of the ping time ahead of it's answer when we get the answer (ie. should now be 3000 + 250 = 3250).
                
                3000 - 5000 + 250 = -1750 is the estimated offset
                5000 - 1750 = 3250 which we think the time is at this moment at the client.
    
                Of course this is a best guess estimate, by doing multiple measurements and taking the average we can close in on the truth.
            */
            int estimatedGetTicksOffset = (int) (ClientTicksMsec - OS.GetTicksMsec() + pingOneWay);
            PushPlayerEstimatedTicksMSecToList(Multiplayer.GetRpcSenderId(), estimatedGetTicksOffset);
            CalculatePlayerEstimatedTicksMSecOffset(Multiplayer.GetRpcSenderId());

            // Check if we are done with the initial request burst
            if (RequestNumber < GetInitialMeasurementCount())
            {
                SendRequestToPlayer(Multiplayer.GetRpcSenderId(), ++RequestNumber);
            }
            else
            {
                StartClientPingCycle(Multiplayer.GetRpcSenderId());
            }
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
                ClientSynchList.Clear();
            }
        }

        [PuppetSync]
        private void UnpauseAtTickMsec(uint UnpauseTime, uint GameTickToUnpauseAt)
        {
            float waitTime = (UnpauseTime - OS.GetTicksMsec()) / 1000f;
            MDLog.Trace(LOG_CAT, $"Unpausing game in {waitTime}");
            Timer timer = CreateUnpausableTimer(RESUME_TIMER_NAME, true, waitTime, true, this,
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
            if (!CompletedNodeSyncList.Contains(Multiplayer.GetRpcSenderId()))
            {
                CompletedNodeSyncList.Add(Multiplayer.GetRpcSenderId());
            }
        }

        #endregion

        #region SUPPORTING METHODS

        ///<summary>Sends a request to the given player for the OS.GetTicksMsec time</summary>
        private void SendRequestToPlayer(int PeerId, int RequestNumber)
        {
            RpcId(PeerId, nameof(RequestTicksMsec), RequestNumber, OS.GetTicksMsec());
        }

        ///<summary>Adds the estimated ticks msec to the players list</summary>
        private void PushPlayerEstimatedTicksMSecToList(int PeerId, int EstimatedTicksMsec)
        {
            if (!InternalTicksList.ContainsKey(PeerId))
            {
                InternalTicksList.Add(PeerId, new List<int>());
            }

            MDLog.Trace(LOG_CAT, $"Peer [{PeerId}] recorded a estimated msec of {EstimatedTicksMsec}");
            InternalTicksList[PeerId].Add(EstimatedTicksMsec);
        }

        ///<summary>Adds the ping to the players ping list and removes any overflow</summary>
        private void PushPlayerPingToQueue(int PeerId, int Ping)
        {
            if (!InternalPingList.ContainsKey(PeerId))
            {
                InternalPingList.Add(PeerId, new Queue<int>());
            }

            InternalPingList[PeerId].Enqueue(Ping);
            MDLog.Trace(LOG_CAT, $"Peer [{PeerId}] recorded a ping of {Ping}");
            if (InternalPingList[PeerId].Count > GetPingsToKeepForAverage())
            {
                InternalPingList[PeerId].Dequeue();
            }

            CalculatePlayerPing(PeerId);
        }

        ///<summary>Calculate the player average ping</summary>
        private void CalculatePlayerPing(int PeerId)
        {
            int estimate = InternalPingList[PeerId].Sum();

            estimate /= InternalPingList[PeerId].Count;
            SetPlayerPing(PeerId, estimate);
        }


        ///<summary>Calculate the player estimated offset for OS.GetTicksMSec</summary>
        private void CalculatePlayerEstimatedTicksMSecOffset(int PeerId)
        {
            int totalEstimate = InternalTicksList[PeerId].Sum();
            totalEstimate /= InternalTicksList[PeerId].Count;
            SetPlayerEstimatedOffset(PeerId, totalEstimate);
            MDLog.Debug(LOG_CAT,
                $"Estimated OS.GetTicksMsec offset for peer [{PeerId}] is {totalEstimate} based on {InternalTicksList[PeerId].Count} measurements");
        }

        ///<summary>Set the estimated offset for the player</summary>
        private void SetPlayerEstimatedOffset(int PeerId, int Estimate)
        {
            if (!PlayerTicksMsecOffset.ContainsKey(PeerId))
            {
                PlayerTicksMsecOffset.Add(PeerId, Estimate);
            }
            else
            {
                PlayerTicksMsecOffset[PeerId] = Estimate;
            }
        }

        ///<summary>Set the ping for the player</summary>
        private void SetPlayerPing(int PeerId, int Ping)
        {
            if (!PlayerPing.ContainsKey(PeerId))
            {
                PlayerPing.Add(PeerId, Ping);
            }
            else
            {
                PlayerPing[PeerId] = Ping;
            }

            OnPlayerPingUpdatedEvent(PeerId, Ping);
        }

        private void OnPlayerJoinedEvent(int PeerId)
        {
            // Check if this is our own join message
            if (PeerId == MDStatics.GetPeerId())
            {
                return;
            }

            // Check if we are a client and leave
            if (MDStatics.IsClient())
            {
                return;
            }

            // Send our first request and start timer
            if (GetInitialMeasurementCount() > 0)
            {
                SendRequestToPlayer(PeerId, 1);
            }

            if (PeerId != MDStatics.GetServerId())
            {
                PauseGame();
                OnSynchStartedEvent(IsPauseOnJoin());
                foreach (int peerid in GameSession.GetAllPeerIds().Where(peerid => peerid != MDStatics.GetServerId()))
                {
                    // Synch just started so set everyone to 0%
                    OnPlayerSynchStatusUpdateEvent(peerid, 0f);

                    // Don't do this for the connecting peer or the server
                    if (PeerId != peerid)
                    {
                        RpcId(peerid, nameof(PauseGame));
                        RpcId(peerid, nameof(RpcReceiveNodeCount), NodeList.Count);
                    }
                }
            }

            RpcId(PeerId, nameof(RpcReceiveNodeCount), NodeList.Count);

            // Start synch check timer
            Timer timer = (Timer) GetNodeOrNull(ALL_PLAYERS_SYNCHED_TIMER_NAME);
            if (timer == null)
            {
                timer = CreateUnpausableTimer(ALL_PLAYERS_SYNCHED_TIMER_NAME, false, SYNCH_TIMER_CHECK_INTERVAL, true,
                    this, nameof(CheckAllClientsSynched));
                timer.Start();
            }
        }

        private void OnPlayerLeftEvent(int PeerId)
        {
            InternalPingList.Remove(PeerId);
            InternalTicksList.Remove(PeerId);
            PlayerTicksMsecOffset.Remove(PeerId);
            PlayerPing.Remove(PeerId);
            CompletedNodeSyncList.Remove(PeerId);
            ClientSynchList.Remove(PeerId);
            MDOnScreenDebug.RemoveOnScreenDebugInfo("Ping(" + PeerId + ")");
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
            }

            // Reset to tick 0 at start of session
            GameClock?.SetCurrentTick(0);
        }

        ///<summary>Starts the player ping request cycle</summary>
        private void StartClientPingCycle(int PeerId)
        {
            if (!IsActivePingEnabled())
            {
                return;
            }

            if (!InternalPingList.ContainsKey(PeerId))
            {
                InternalPingList.Add(PeerId, new Queue<int>());
            }

            MDOnScreenDebug.AddOnScreenDebugInfo($"Ping({PeerId})",
                () => MDStatics.GetGameSynchronizer().GetPlayerPing(PeerId).ToString());
            Timer timer = CreateUnpausableTimer($"PingTimer{PeerId}", false, GetPingInterval(), true, this,
                nameof(OnPingTimerTimeout), PeerId);
            timer.Start();
        }

        private void OnPingTimerTimeout(Timer timer, int PeerId)
        {
            // Check if peer is still active
            if (!InternalPingList.ContainsKey(PeerId) || !MDStatics.IsNetworkActive())
            {
                MDLog.Trace(LOG_CAT, $"Peer {PeerId} has disconnected, stopping ping");
                timer.Stop();
                timer.RemoveAndFree();
                return;
            }

            // Send ping request
            if (GameClock == null)
            {
                RpcId(PeerId, nameof(RequestPing), OS.GetTicksMsec());
            }
            else
            {
                uint ping = (uint) GetPlayerPing(PeerId);
                int maxPlayerPing = GetMaxPlayerPing() + (int) ping;
                uint estimate = GetPlayerTicksMsec(PeerId) + ping;
                RpcId(PeerId, nameof(RequestPing), OS.GetTicksMsec(), estimate, GameClock.GetTickAtTimeOffset(ping),
                    maxPlayerPing);
            }
        }

        private void CheckAllClientsSynched(Timer timer)
        {
            if (ClientSynchList.Count < GameSession.GetAllPlayerInfos().Count - 1)
            {
                MDLog.Debug(LOG_CAT, "All clients are not synched yet");
                return;
            }

            // Double check each client, if another client left while we were synching
            // We could believe we are synched while we really are not
            bool allClientsSynched = true;
            foreach (int peerId in GameSession.GetAllPeerIds())
            {
                if (peerId == MDStatics.GetServerId()
                    || ClientSynchList.Contains(peerId) && PlayerTicksMsecOffset.ContainsKey(peerId))
                {
                    continue;
                }

                MDLog.Trace(LOG_CAT, $"Peer [{peerId}] is not yet fully synched");
                allClientsSynched = false;
            }

            if (!allClientsSynched)
            {
                MDLog.Debug(LOG_CAT, "All peers are not synched yet");
                return;
            }

            bool waitForTickMsec = false;

            // Check if we still need to wait for a better confidence on the TickMsec value
            foreach (int peerId in GameSession.GetAllPeerIds())
            {
                if (peerId == MDStatics.GetServerId()
                    || InternalTicksList.ContainsKey(peerId) &&
                    InternalTicksList[peerId].Count >= GetMinimumMeasurementCountBeforeResume())
                {
                    continue;
                }

                waitForTickMsec = true;
                MDLog.Trace(LOG_CAT, $"Still waiting for peer [{peerId}] to get a more secure TickMsec value");
            }

            if (waitForTickMsec)
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
            ClientSynchList.Clear();
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

        private Timer CreateUnpausableTimer(string TimerName, bool OneShot, float WaitTime, bool TimerAsFirstArgument,
            Godot.Object ConnectionTarget, string MethodName, params object[] Parameters)
        {
            Timer timer = this.CreateTimer(TimerName, OneShot, WaitTime, TimerAsFirstArgument, ConnectionTarget,
                MethodName,
                Parameters);
            timer.PauseMode = PauseModeEnum.Process;
            return timer;
        }

        #endregion

        #region VIRTUAL METHODS

        /// <summary>Pauses the game for synching on player join (Default: True)</summary>
        protected virtual bool IsPauseOnJoin()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfiugrationSections.GameSynchronizer, "PauseOnJoin", true);
        }

        /// <summary>Delay MDReplicator until all nodes are synched (Default: False)</summary>
        public virtual bool IsDelayReplicatorUntilAllNodesAreSynched()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfiugrationSections.GameSynchronizer, "DelayReplicatorUntilAllNodesSynched", false);
        }

        /// <summary>Unpause countdown duration (Default: 2 seconds)</summary>
        protected virtual uint GetUnpauseCountdownDurationMSec()
        {
            return (uint) this.GetConfiguration().GetInt(MDConfiguration.ConfiugrationSections.GameSynchronizer, "UnpauseCountdownDuration", 2000);
        }

        /// <summary>How often do we ping each client (Default: 0.5f)</summary>
        protected virtual float GetPingInterval()
        {
            return float.Parse(this.GetConfiguration().GetString(MDConfiguration.ConfiugrationSections.GameSynchronizer, "PingInterval", "0.5"));
        }

        /// <summary>Pings to keep for getting average (Default: 10)</summary>
        protected virtual int GetPingsToKeepForAverage()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfiugrationSections.GameSynchronizer, "PingsToKeepForAverage", 10);
        }

        /// <summary>If set to true we will ping every player continuously. (Default: true)
        /// <para>You can set the interval with <see cref="GetPingInterval"/></para></summary>
        protected virtual bool IsActivePingEnabled()
        {
            return this.GetConfiguration().GetBool(MDConfiguration.ConfiugrationSections.GameSynchronizer, "ActivePingEnabled", true);
        }

        /// <summary>This decides how many times we go back and forth to establish the OS.GetTicksMsec offset for each client (Default: 20)</summary>
        protected virtual int GetInitialMeasurementCount()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfiugrationSections.GameSynchronizer, "InitialMeasurementCount", 20);
        }

        /// <summary>If IsPauseOnJoin() is enabled we will wait for at least this level of security for TicksMsec before we resume (Default: GetInitialMeasurementCount() / 2)</summary>
        protected virtual int GetMinimumMeasurementCountBeforeResume()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfiugrationSections.GameSynchronizer, "InitialMeasurementCountBeforeResume", 10);
        }

        #endregion
    }
}