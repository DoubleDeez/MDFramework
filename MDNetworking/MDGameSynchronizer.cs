using Godot;
using System;
using System.Collections.Generic;

///<summary>A synchronization class with three primary features
///<para>It tracks the ping to each client</para><para></para>
///<para>It tracks what the local OS.GetTicksMsec() is on each connected client relative to the server (server only)</para><para></para>
///<para>Finally it takes care of making sure all players are fully synchronized on request. 
/// Usually when a player joins or level is changed</para>
///</summary>
public class MDGameSynchronizer : Node
{
    private const string LOG_CAT = "LogGameSynchronizer";

    public MDGameInstance GameInstance = null;

    public MDGameSession GameSession = null;

    ///<Summary>Stores the latest ping response times for each player</summary>
    protected Dictionary<int, Queue<float>> InternalPingList = new Dictionary<int, Queue<float>>();

    ///<Summary>Stores all the estimated GetTicksMsec offsets for all players</summary>
    protected Dictionary<int, List<int>> InternalTicksList = new Dictionary<int, List<int>>();

    ///<Summary>This contains what we think is the GetTicksMsec offset for each player</summary>
    protected Dictionary<int, int> PlayerTicksMsecOffset = new Dictionary<int, int>();

    ///<Summary>This contains what we think is the ping for each player</summary>
    protected Dictionary<int, int> PlayerPing = new Dictionary<int, int>();



    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GameSession = GameInstance.GetGameSession();
        GameSession.OnPlayerJoinedEvent += OnPlayerJoinedEvent;
        GameSession.OnPlayerLeftEvent += OnPlayerLeftEvent;
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

#endregion

#region RPC METHODS

    [Puppet]
    protected void RequestPing(uint ServerTimeOfRequest)
    {
        // Respond
        RpcId(GameSession.GetNetworkMaster(), nameof(PingResponse), MDStatics.GetPeerId(), ServerTimeOfRequest);
        MDLog.Info(LOG_CAT, "Responded to server request for ping");
    }

    [Master]
    protected void PingResponse(int PeerId, uint ServerTimeOfRequest)
    {
        float ping = OS.GetTicksMsec() - ServerTimeOfRequest;
        PushPlayerPingToQueue(PeerId, ping);
    }

    ///<summary>Requests the OS.GetTicksMsec() from the client</summary>
    [Puppet]
    protected void RequestTicksMsec(int RequestNumber, uint ServerTimeOfRequest)
    {
        // Respond
        RpcId(GameSession.GetNetworkMaster(), nameof(ResponseTicksMsec), MDStatics.GetPeerId(), OS.GetTicksMsec(), ServerTimeOfRequest, RequestNumber);
        MDLog.Info(LOG_CAT, "Responded to server request number {0} for OS.GetTicksMsec with [{1}]", RequestNumber, OS.GetTicksMsec());
    }

    ///<summary>Response to our OS.GetTicksMsec() request from the client</summary>
    [Master]
    protected void ResponseTicksMsec(int PeerId, uint ClientTicksMsec, uint ServerTimeOfRequest, int RequestNumber)
    {
        MDLog.Info(LOG_CAT, "Msec response number {0} from peer [{1}] is {2} local Msec is {3}", RequestNumber, PeerId, ClientTicksMsec, OS.GetTicksMsec());
        // Get and record ping
        float ping = OS.GetTicksMsec() - ServerTimeOfRequest;
        PushPlayerPingToQueue(PeerId, ping);

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
        PushPlayerEstimatedTicksMSecToList(PeerId, estimatedGetTicksOffset);
        CalculatePlayerEstimatedTicksMSecOffset(PeerId);

        // Check if we are done with the initial request burst
        if (RequestNumber < GetInitialMeasurementCount())
        {
            SendRequestToPlayer(PeerId, ++RequestNumber);
        }
        else
        {
            StartClientPingCycle(PeerId);
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

        MDLog.Info(LOG_CAT, "Peer [{0}] recorded a estimated msec of {1}", PeerId, EstimatedTicksMSec);
        InternalTicksList[PeerId].Add(EstimatedTicksMSec);
    }

    ///<summary>Adds the ping to the players ping list and removes any overflow</summary>
    private void PushPlayerPingToQueue(int PeerId, float Ping)
    {
        if (!InternalPingList.ContainsKey(PeerId))
        {
            InternalPingList.Add(PeerId, new Queue<float>());
        }

        InternalPingList[PeerId].Enqueue(Ping);
        MDLog.Info(LOG_CAT, "Peer [{0}] recorded a ping of {1}", PeerId, Ping);
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
        // TODO: Improve this to remove outliers?
        int totalEstimate = 0;
        InternalTicksList[PeerId].ForEach(val => totalEstimate += val);
        totalEstimate = totalEstimate / InternalTicksList[PeerId].Count;
        SetPlayerEstimatedOffset(PeerId, totalEstimate);
        MDLog.Info(LOG_CAT, "Estimated OS.GetTicksMsec offset for peer [{0}] is {1} based on {2} measurements", PeerId, totalEstimate, InternalTicksList[PeerId].Count);
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
    }

    private void OnPlayerJoinedEvent(int PeerId)
    {
        // Check if this is our own join message or if we are a client
        if (PeerId == MDStatics.GetPeerId() || MDStatics.IsClient())
        {
            return;
        }

        // Send our first request and start timer
        if (GetInitialMeasurementCount() > 0)
        {
            SendRequestToPlayer(PeerId, 1);
        }
    }

    private void OnPlayerLeftEvent(int PeerId)
    {
        InternalPingList.Remove(PeerId);
        InternalTicksList.Remove(PeerId);
        PlayerTicksMsecOffset.Remove(PeerId);
        PlayerPing.Remove(PeerId);
    }

    ///<summary>Starts the player ping request cycle</summary>
    private void StartClientPingCycle(int PeerId)
    {
        if (IsActivePingEnabled())
        {
            Timer t = new Timer();
            t.Name = "PingTimer" + PeerId;
            t.OneShot = false;
            t.WaitTime = GetPingInterval();
            List<object> parameters = new List<object>();
            parameters.Add(t);
            parameters.Add(PeerId);
            t.Connect("timeout", this, nameof(OnPingTimerTimeout), new Godot.Collections.Array(parameters));
            AddChild(t);
            t.Start();
        }
    }

    private void OnPingTimerTimeout(Timer timer, int PeerId)
    {
        // Check if peer is still active
        if (!InternalPingList.ContainsKey(PeerId) || !MDStatics.IsNetworkActive())
        {
            MDLog.Info(LOG_CAT, "Peer {0} has disconnected, stopping ping", PeerId);
            timer.Stop();
            timer.QueueFree();
            return;
        }

        // Send ping request
        RpcId(PeerId, nameof(RequestPing), OS.GetTicksMsec());
    }

#endregion

#region VIRTUAL METHODS

    /// <summary>How often do we ping each client (Default: Every second)</summary>
    protected virtual float GetPingInterval()
    {
        return 1f;
    }

    /// <summary>Pings to keep for getting average (Default: 10)</summary>
    protected virtual int GetPingsToKeepForAverage()
    {
        return 10;
    }

    /// <summary>If set to true we will ping every player continuously.(Default: true)
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

#endregion
}
