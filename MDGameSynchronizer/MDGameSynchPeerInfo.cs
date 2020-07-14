using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// This class is used by the GameSynchronizer to keep track of a single peer.
    /// </summary>
    public class MDGameSynchPeerInfo
    {
        private const string LOG_CAT = "LogGameSynchPeerInfo";

        // We don't want to have to read config all the time
        protected int SettingAveragePingToKeep = 0;

        /// <summary>List of estimated ping times</summary>
        protected Queue<int> PingList = new Queue<int>();

        /// <summary>List of estiamted OS.GetTicksMSec offsets</summary>
        protected List<int> TicksList = new List<int>();

        /// <summary>The average estimated OS.GetTicksMSec offset for this peer</summary>
        public int TickMSecOffset { get; set; }

        /// <summary>The average estimated ping for this peer</summary>
        public int Ping { get; set;}

        /// <summary>Has this peer completed the node sync</summary>
        public bool CompletedNodeSynch { get; set;}

        /// <summary>Has this peer completed synching</summary>
        public bool CompletedSynch { get; set;}

        public int PeerId {get; private set; }

        protected MDGameSynchronizer GameSynchronizer;

        protected Timer PingTimer;

        public MDGameSynchPeerInfo(MDGameSynchronizer GameSynchronizer, int PeerId)
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Force));
            MDLog.Info(LOG_CAT, $"Creating MDGameSynchPeerInfo for Peer [ID: {PeerId}]");
            this.GameSynchronizer = GameSynchronizer;
            this.PeerId = PeerId;
            this.SettingAveragePingToKeep = GameSynchronizer.GetPingsToKeepForAverage();
            this.CompletedNodeSynch = false;
            this.CompletedSynch = false;
            StartMSecCycle();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Dispose()
        {
            MDOnScreenDebug.RemoveOnScreenDebugInfo($"Ping({PeerId})");
            if (Godot.Object.IsInstanceValid(PingTimer))
            {
                PingTimer.Stop();
                PingTimer.RemoveAndFree();
            }
        }

        #region Public Methods

        /// <summary>
        /// Called when a response for OS.GetTicksMsec() comes back
        /// </summary>
        /// <param name="ClientTicksMsec">The Os.GetTicksMsec() time at the client when they sent the response</param>
        /// <param name="ServerTimeOfRequest">The time we sent the request to the client</param>
        /// <param name="RequestNumber">The request number of this request</param>
        public void ProcessMSecResponse(uint ClientTicksMsec, uint ServerTimeOfRequest, int RequestNumber)
        {
            // Get and record ping
            int ping = (int) (OS.GetTicksMsec() - ServerTimeOfRequest);
            PushPlayerPingToQueue(ping);

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
            PushPlayerEstimatedTicksMSecToList(estimatedGetTicksOffset);

            // Check if we are done with the initial request burst
            if (RequestNumber < GameSynchronizer.GetInitialMeasurementCount())
            {
                SendRequestToPlayer(++RequestNumber);
            }
            else
            {
                StartClientPingCycle();
            }
        }

        /// <summary>
        /// Called when the ping timer times out, sends a ping request to the given client.
        /// </summary>
        public void OnPingTimerTimeout()
        {
            // Check if network is still active
            if (!MDStatics.IsNetworkActive())
            {
                MDLog.Trace(LOG_CAT, $"Network is no longer active");
                return;
            }

            // Send ping request
            if (GameSynchronizer.GameClock == null)
            {
                GameSynchronizer.RpcId(PeerId, MDGameSynchronizer.METHOD_REQUEST_PING, OS.GetTicksMsec());
            }
            else
            {
                int maxPlayerPing = GameSynchronizer.GetMaxPlayerPing() + (int) Ping;
                uint estimate = GameSynchronizer.GetPlayerTicksMsec(PeerId) + (uint)Ping;
                GameSynchronizer.RpcId(PeerId, MDGameSynchronizer.METHOD_REQUEST_PING, OS.GetTicksMsec(), estimate, 
                                GameSynchronizer.GameClock.GetTickAtTimeOffset(Ping), maxPlayerPing);
            }
        }

        ///<summary>Adds the ping to the players ping list and removes any overflow</summary>
        public void PushPlayerPingToQueue(int Ping)
        {
            PingList.Enqueue(Ping);
            MDLog.Trace(LOG_CAT, $"Peer [{PeerId}] recorded a ping of {Ping}");
            if (PingList.Count > SettingAveragePingToKeep)
            {
                PingList.Dequeue();
            }

            CalculatePlayerAveragePing();
        }

        /// <summary>
        /// Check if we got enough of a confidence value in the Msec value of the client to resume the game
        /// </summary>
        /// <returns>True if we are ready to resume, false if not</returns>
        public bool IsClientMSecConfident()
        {
            if (TicksList.Count >= GameSynchronizer.GetMinimumMeasurementCountBeforeResume())
            {
                return true;
            }
            
            MDLog.Trace(LOG_CAT, $"Still waiting for peer [{PeerId}] to get a more secure TickMsec value");
            return false;
        }

        #endregion

        #region Network Methods

        private void StartMSecCycle()
        {
            // Sends a request to the given player for the OS.GetTicksMsec time
            if (GameSynchronizer.GetInitialMeasurementCount() > 0)
            {
                SendRequestToPlayer(1);
            }
        }

        private void SendRequestToPlayer(int ReuqestNumber)
        {
            GameSynchronizer.RpcId(PeerId, MDGameSynchronizer.METHOD_REQUEST_TICKS_MSEC, ReuqestNumber, OS.GetTicksMsec());
        }

        #endregion

        #region Support Methods

        ///<summary>Starts the player ping request cycle</summary>
        private void StartClientPingCycle()
        {
            if (!GameSynchronizer.IsActivePingEnabled())
            {
                return;
            }

            // Onscreen debug for ping
            MDOnScreenDebug.AddOnScreenDebugInfo($"Ping({PeerId})",
                () => MDStatics.GetGameSynchronizer().GetPlayerPing(PeerId).ToString());

            PingTimer = GameSynchronizer.CreateUnpausableTimer($"PingTimer{PeerId}", false, GameSynchronizer.GetPingInterval(), 
                                            false, GameSynchronizer, MDGameSynchronizer.METHOD_ON_PING_TIMER_TIMEOUT, PeerId);
            PingTimer.Start();
        }

        ///<summary>Calculate the player average ping</summary>
        private void CalculatePlayerAveragePing()
        {
            int estimate = PingList.Sum();
            estimate /= PingList.Count;

            Ping = estimate;
            GameSynchronizer.SendPlayerPingEvent(PeerId, Ping);
        }

        ///<summary>Adds the estimated ticks msec to the players list</summary>
        private void PushPlayerEstimatedTicksMSecToList(int EstimatedTicksMsec)
        {
            MDLog.Trace(LOG_CAT, $"Peer [{PeerId}] recorded a estimated msec of {EstimatedTicksMsec}");
            TicksList.Add(EstimatedTicksMsec);
            CalculatePlayerEstimatedTicksMSecOffset();
        }


        ///<summary>Calculate the player estimated offset for OS.GetTicksMSec</summary>
        private void CalculatePlayerEstimatedTicksMSecOffset()
        {
            int estimate = TicksList.Sum();
            estimate /= TicksList.Count;

            TickMSecOffset = estimate;
            MDLog.Debug(LOG_CAT,
                $"Estimated OS.GetTicksMsec offset for peer [{PeerId}] is {estimate} based on {TicksList.Count} measurements");
        }

        #endregion
    }
}
