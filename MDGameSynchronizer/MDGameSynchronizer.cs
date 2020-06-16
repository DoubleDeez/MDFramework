using Godot;
using System;
using System.Collections.Generic;

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
    public event SynchStartedHandler OnSynchStartedEvent = delegate {};

    public delegate void SynchCompleteHandler(float ResumeGameIn);
    public event SynchCompleteHandler OnSynchCompleteEvent = delegate {};

    public delegate void SynchStatusUpdateHandler(int PeerId, float ProgressPercentage);
    public event SynchStatusUpdateHandler OnPlayerSynchStatusUpdateEvent = delegate {};

    public delegate void SynchPlayerPingUpdatedHandler(int PeerId, int Ping);
    public event SynchPlayerPingUpdatedHandler OnPlayerPingUpdatedEvent = delegate {};

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
        if (!PlayerPing.ContainsKey(PeerId))
        {
            MDLog.Warn(LOG_CAT, "Requested ping for peer [{0}] that doesn't exist in list", PeerId);
            return -1;
        }
        return PlayerPing[PeerId];
    }

    ///<summary>Returns the highest ping we got to any player</summary>
    public int GetMaxPlayerPing()
    {
        int highest = 0;
        foreach (int ping in PlayerPing.Values)
        {
            highest = Math.Max(ping, highest);
        }
        return highest;
    }

    /// <summary> Returns the estimated OS.GetTicksMsec for the given peer or 0 if the peer does not exist.</summary>
    public uint GetPlayerTicksMsec(int PeerId)
    {
        if (!PlayerTicksMsecOffset.ContainsKey(PeerId))
        {
            MDLog.Warn(LOG_CAT, "Requested OS.GetTicksMsec for peer [{0}] that doesn't exist in list", PeerId);
            return 0;
        }
        return Convert.ToUInt32(OS.GetTicksMsec() + PlayerTicksMsecOffset[PeerId]);
    }

    public bool HasPeerCompletedNodeSynch(int PeerId)
    {
        return CompletedNodeSyncList.Contains(PeerId);
    }

#endregion

#region RPC METHODS

    [Puppet]
    protected void RpcRecieveNodeCount(int NodeCount)
    {
        MDLog.Debug(LOG_CAT, "Total nodes that need synch are {0}", NodeCount);
        this.NodeCount = NodeCount;
        NodeSynchCompleted = false;
        // Start synch timer
        Timer timer = CreateTimer("SynchTimer", false, SYNCH_TIMER_CHECK_INTERVAL, true, this, nameof(CheckSynchStatus));
        timer.Start();

        // Send out synch started event
        OnSynchStartedEvent(IsPauseOnJoin());
        // Set all peers to 0% except server
        foreach (int peerid in GameSession.GetAllPeerIds())
        {
            if (peerid != MDStatics.GetServerId())
            {
                OnPlayerSynchStatusUpdateEvent(peerid, 0f);
            }
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
        MDLog.Debug(LOG_CAT, "Peer [{0}] completed synch", Multiplayer.GetRpcSenderId());
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
        MDLog.Debug(LOG_CAT, "Peer [{0}] has synched {1} out of {2} nodes", Multiplayer.GetRpcSenderId(), SynchedNodes, NodeList.Count);

        // Send status update to all players so they can update UI
        UpdateSynchStatusOnAllClients(Multiplayer.GetRpcSenderId(), (float)SynchedNodes / NodeList.Count);

        // Update our own UI
        OnPlayerSynchStatusUpdateEvent(Multiplayer.GetRpcSenderId(), (float)SynchedNodes / NodeList.Count);
    }

    [Remote]
    protected void RequestPingAndUpdateClock(uint ServerTimeOfRequest)
    {
        // Respond
        this.RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
        MDLog.Trace(LOG_CAT, "Responded to server request for ping");
    }

    ///<Summary>Sent by server when requesting ping, also keeping game clock in sync</summary>
    [Puppet]
    protected void RequestPing(uint ServerTimeOfRequest, uint EstimateTime, uint EstimatedTick)
    {
        // Respond
        RequestPing(ServerTimeOfRequest);
        if (GameClock != null)
        {
            GameClock.CheckSynch(EstimateTime, EstimatedTick);
        }
    }

    [Remote]
    ///<Summary>Sent by client to request ping or when gameclock is inactive</summary>
    protected void RequestPing(uint ServerTimeOfRequest)
    {
        // Respond
        this.RpcId(Multiplayer.GetRpcSenderId(), nameof(PingResponse), ServerTimeOfRequest);
        MDLog.Trace(LOG_CAT, "Responded to server request for ping");
    }

    [Remote]
    protected void PingResponse(uint ServerTimeOfRequest)
    {
        int ping = (int)(OS.GetTicksMsec() - ServerTimeOfRequest);
        PushPlayerPingToQueue(Multiplayer.GetRpcSenderId(), ping);
    }

    ///<summary>Requests the OS.GetTicksMsec() from the client</summary>
    [Puppet]
    protected void RequestTicksMsec(int RequestNumber, uint ServerTimeOfRequest)
    {
        // Respond
        this.MDServerRpc(nameof(ResponseTicksMsec), OS.GetTicksMsec(), ServerTimeOfRequest, RequestNumber);
        MDLog.Trace(LOG_CAT, "Responded to server request number {0} for OS.GetTicksMsec with [{1}]", RequestNumber, OS.GetTicksMsec());
    }

    ///<summary>Response to our OS.GetTicksMsec() request from the client</summary>
    [Master]
    protected void ResponseTicksMsec(uint ClientTicksMsec, uint ServerTimeOfRequest, int RequestNumber)
    {
        MDLog.Debug(LOG_CAT, "Msec response number {0} from peer [{1}] is {2} local Msec is {3}", RequestNumber, Multiplayer.GetRpcSenderId(), ClientTicksMsec, OS.GetTicksMsec());
        // Get and record ping
        int ping = (int)(OS.GetTicksMsec() - ServerTimeOfRequest);
        PushPlayerPingToQueue(Multiplayer.GetRpcSenderId(), ping);

        // Calculate ping for one way trip (Ping / 2)
        long pingOneWay = (long)(Mathf.Floor(ping) / 2f);

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
        int estimatedGetTicksOffset = (int)((long)ClientTicksMsec - (long)OS.GetTicksMsec() + pingOneWay);
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
        if (IsPauseOnJoin())
        {
            GetTree().Paused = true;
            
            // Check if resume timer is active, if so kill it
            Timer resumeTimer = (Timer)GetNodeOrNull(RESUME_TIMER_NAME);
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
    }

    [PuppetSync]
    private void UnpauseAtTickMsec(uint UnpauseTime, uint GameTickToUnpauseAt)
    {
        float waitTime = ((float)(UnpauseTime - OS.GetTicksMsec())) / 1000f;
        MDLog.Trace(LOG_CAT, "Unpausing game in {0}", waitTime);
        Timer timer = CreateTimer(RESUME_TIMER_NAME, true, waitTime, true, this, nameof(OnUnpauseTimerTimeout));
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
        MDLog.Trace(LOG_CAT, "Peer [{0}] has completed node synchronization", Multiplayer.GetRpcSenderId());
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
    private void PushPlayerEstimatedTicksMSecToList(int PeerId, int EstimatedTicksMSec)
    {
        if (!InternalTicksList.ContainsKey(PeerId))
        {
            InternalTicksList.Add(PeerId, new List<int>());
        }

        MDLog.Trace(LOG_CAT, "Peer [{0}] recorded a estimated msec of {1}", PeerId, EstimatedTicksMSec);
        InternalTicksList[PeerId].Add(EstimatedTicksMSec);
    }

    ///<summary>Adds the ping to the players ping list and removes any overflow</summary>
    private void PushPlayerPingToQueue(int PeerId, int Ping)
    {
        if (!InternalPingList.ContainsKey(PeerId))
        {
            InternalPingList.Add(PeerId, new Queue<int>());
        }

        InternalPingList[PeerId].Enqueue(Ping);
        MDLog.Trace(LOG_CAT, "Peer [{0}] recorded a ping of {1}", PeerId, Ping);
        if (InternalPingList[PeerId].Count > GetPingsToKeepForAverage())
        {
            InternalPingList[PeerId].Dequeue();
        }

        CalculatePlayerPing(PeerId);
    }

    ///<summary>Calculate the player average ping</summary>
    private void CalculatePlayerPing(int PeerId)
    {
        int estimate = 0;
        foreach (int ping in InternalPingList[PeerId])
        {
            estimate += ping;
        }
        estimate = estimate / InternalPingList[PeerId].Count;
        SetPlayerPing(PeerId, estimate);
    }


    ///<summary>Calculate the player estimated offset for OS.GetTicksMSec</summary>
    private void CalculatePlayerEstimatedTicksMSecOffset(int PeerId)
    {
        int totalEstimate = 0;
        InternalTicksList[PeerId].ForEach(val => totalEstimate += val);
        totalEstimate = totalEstimate / InternalTicksList[PeerId].Count;
        SetPlayerEstimatedOffset(PeerId, totalEstimate);
        MDLog.Debug(LOG_CAT, "Estimated OS.GetTicksMsec offset for peer [{0}] is {1} based on {2} measurements", PeerId, totalEstimate, InternalTicksList[PeerId].Count);
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

        // Check if we are a client
        if (MDStatics.IsClient())
        {
            // Track ping to other players
            StartClientPingCycle(PeerId);
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
            foreach (int peerid in GameSession.GetAllPeerIds())
            {
                // Skip the server
                if (peerid == MDStatics.GetServerId())
                {
                    continue;
                }

                // Synch just started so set everyone to 0%
                OnPlayerSynchStatusUpdateEvent(peerid, 0f);

                // Don't do this for the connecting peer or the server
                if (PeerId != peerid)
                {
                    RpcId(peerid, nameof(PauseGame));
                    RpcId(peerid, nameof(RpcRecieveNodeCount), NodeList.Count);
                }
            }
        }
        RpcId(PeerId, nameof(RpcRecieveNodeCount), NodeList.Count);

        // Start synch check timer
        Timer timer = (Timer)GetNodeOrNull(ALL_PLAYERS_SYNCHED_TIMER_NAME);
        if (timer == null)
        {
            timer = CreateTimer(ALL_PLAYERS_SYNCHED_TIMER_NAME, false, SYNCH_TIMER_CHECK_INTERVAL, true, this, nameof(CheckAllClientsSynched));
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
            StartClientPingCycle(MDStatics.GetServerId());
            PauseGame();
        }

        if (GameClock != null)
        {
            // Reset to tick 0 at start of session
            GameClock.SetCurrentTick(0);
        }
    }

    ///<summary>Starts the player ping request cycle</summary>
    private void StartClientPingCycle(int PeerId)
    {
        if (IsActivePingEnabled())
        {
            if (!InternalPingList.ContainsKey(PeerId))
            {
                InternalPingList.Add(PeerId, new Queue<int>());
            }
            MDOnScreenDebug.AddOnScreenDebugInfo("Ping(" + PeerId + ")", () => MDStatics.GetGameSynchronizer().GetPlayerPing(PeerId).ToString());
            Timer timer = CreateTimer("PingTimer" + PeerId, false, GetPingInterval(), true, this, nameof(OnPingTimerTimeout), PeerId);
            timer.Start();
        }
    }

    private void OnPingTimerTimeout(Timer timer, int PeerId)
    {
        // Check if peer is still active
        if (!InternalPingList.ContainsKey(PeerId) || !MDStatics.IsNetworkActive())
        {
            MDLog.Trace(LOG_CAT, "Peer {0} has disconnected, stopping ping", PeerId);
            timer.Stop();
            timer.RemoveAndFree();
            return;
        }

        // Send ping request
        if (MDStatics.IsClient() || GameClock == null)
        {
            RpcId(PeerId, nameof(RequestPing), OS.GetTicksMsec());
        }
        else
        {
            uint ping = (uint)GetPlayerPing(PeerId);
            uint estimate = GetPlayerTicksMsec(PeerId) + ping;
            RpcId(PeerId, nameof(RequestPing), OS.GetTicksMsec(), estimate, GameClock.GetTickAtTimeOffset(ping));
        }
    }

    private void CheckAllClientsSynched(Timer timer)
    {
        if (ClientSynchList.Count < GameSession.GetAllPlayerInfos().Count-1)
        {
            MDLog.Debug(LOG_CAT, "All clients are not synched yet");
            return;
        }

        // Double check each client, if another client left while we were synching
        // We could believe we are synched while we really are not
        bool allClientsSynched = true;
        foreach (int peerId in GameSession.GetAllPeerIds())
        {
            if (peerId == MDStatics.GetServerId())
            {
                continue;
            }
            if (!ClientSynchList.Contains(peerId) || !PlayerTicksMsecOffset.ContainsKey(peerId))
            {
                MDLog.Trace(LOG_CAT, "Peer [{0}] is not yet fully synched", peerId);
                allClientsSynched = false;
            }
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
            if (peerId == MDStatics.GetServerId())
            {
                continue;
            }
            if (!InternalTicksList.ContainsKey(peerId) || InternalTicksList[peerId].Count < GetMinimumMeasurementCountBeforeResume())
            {
                waitForTickMsec = true;
                MDLog.Trace(LOG_CAT, "Still waiting for peer [{0}] to get a more secure TickMsec value", peerId);
                continue;
            }
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
            uint tickToUnpause = GameClock != null ? GameClock.GetTick() : 0;
            
            if (peerid != MDStatics.GetServerId())
            {
                RpcId(peerid, nameof(UnpauseAtTickMsec), GetPlayerTicksMsec(peerid) + GetUnpauseCountdownDurationMSec(), tickToUnpause);
            }
        }

        UnpauseAtTickMsec(OS.GetTicksMsec() + GetUnpauseCountdownDurationMSec(), 0);
        ClientSynchList.Clear();
        timer.RemoveAndFree();
    }

    private void OnNetworkNodeAdded(Node node)
    {
        MDLog.Trace(LOG_CAT, "Node added: {0}", node.GetPath());
        NodeList.Add(node);
    }

    private void OnNetworkNodeRemoved(Node node)
    {
        MDLog.Trace(LOG_CAT, "Node removed: {0}", node.GetPath());
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
            this.MDServerRpc(nameof(NotifyAllNodesSynched));
        }

        int NotSynchedNodes = 0;

        // Check node custom logic to see if synch is done
        foreach (Node node in NodeList)
        {
            if (node is IMDSynchronizedNode)
            {
                if (!((IMDSynchronizedNode)node).IsSynchronizationComplete())
                {
                    // We are not synched
                    MDLog.Trace(LOG_CAT, "A node is still synching: {0}", node.GetPath());
                    SynchedNodes--;
                    NotSynchedNodes++;
                }
            }
        }

        if (SynchedNodes == NodeCount)
        {
            // We are done synching
            RpcId(MDStatics.GetServerId(), nameof(ClientSynchDone));
            MDLog.Debug(LOG_CAT, "We are done synching notifying server");
            // Set ourselves to done
            OnPlayerSynchStatusUpdateEvent(MDStatics.GetPeerId(), 1f);
            timer.RemoveAndFree();
            return;
        } 
        else
        {
            float percentage = (float)SynchedNodes / NodeCount;
            MDLog.Debug(LOG_CAT, "We have {0} nodes that are still synching. Current status: {1}%", NotSynchedNodes, percentage * 100);
            // Notify the server of how many nodes we got synched
            this.MDServerRpc(nameof(ClientSynchStatus), SynchedNodes);

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

    private Timer CreateTimer(String Name, bool OneShot, float WaitTime, bool TimerAsFirstArgument, Godot.Object ConnectionTarget, String MethodName, params object[] Parameters)
    {
        Timer timer = new Timer();
        timer.Name = Name;
        timer.OneShot = OneShot;
        timer.WaitTime = WaitTime;
        List<object> parameters = new List<object>();
        if (TimerAsFirstArgument)
        {
            parameters.Add(timer);
        }
        foreach (object param in Parameters)
        {
            parameters.Add(param);
        }
        timer.Connect("timeout", ConnectionTarget, MethodName, new Godot.Collections.Array(parameters));
        timer.PauseMode = PauseModeEnum.Process;
        AddChild(timer);
        return timer;
    }

#endregion

#region VIRTUAL METHODS

    /// <summary>Pauses the game for synching on player join (Default: True)</summary>
    protected virtual bool IsPauseOnJoin()
    {
        return true;
    }

    /// <summary>Delay MDReplicator until all nodes are synched (Default: True)</summary>
    public virtual bool IsDelayReplicatorUntilAllNodesAreSynched()
    {
        return false;
    }

    /// <summary>Unpause countdown duration (Default: 2 seconds)</summary>
    protected virtual uint GetUnpauseCountdownDurationMSec()
    {
        return 2000;
    }

    /// <summary>How often do we ping each client (Default: 3f)</summary>
    protected virtual float GetPingInterval()
    {
        return 0.5f;
    }

    /// <summary>Pings to keep for getting average (Default: 10)</summary>
    protected virtual int GetPingsToKeepForAverage()
    {
        return 10;
    }

    /// <summary>If set to true we will ping every player continuously. (Default: true)
    /// <para>You can set the interval with <see cref="GetPingInterval"/></para></summary>
    protected virtual bool IsActivePingEnabled()
    {
        return true;
    }

    /// <summary>This decides how many times we go back and forth to establish the OS.GetTicksMsec offset for each client (Default: 20)</summary>
    protected virtual int GetInitialMeasurementCount()
    {
        return 20;
    }

    /// <summary>Sets if we should use the MDGameClock or not, this requires IsActivePingEnabled to be true. (Default: true)</summary>
    public virtual bool IsGameClockActive()
    {
        return true;
    }

    /// <summary>If IsPauseOnJoin() is enabled we will wait for at least this level of security for TicksMsec before we resume (Default: GetInitialMeasurementCount() / 2)</summary>
    protected virtual int GetMinimumMeasurementCountBeforeResume()
    {
        return GetInitialMeasurementCount() / 2;
    }

#endregion
}
