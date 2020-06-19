using Godot;
using System;
using System.Collections.Generic;

public class MDClockedNetworkDataNode : Node
{
    private static readonly string LOG_CAT = "LogMDClockedNetworkDataNode";

    protected float UpdateInterval = 0.1f;

    protected float _updateCooldown = 0f;

    protected List<IMDClockedNetworkValue> ReliableIntervalValues = new List<IMDClockedNetworkValue>();

    protected List<IMDClockedNetworkValue> UnreliableIntervalValues = new List<IMDClockedNetworkValue>();

    protected List<IMDClockedNetworkValue> OnChangeAndOneShotValues = new List<IMDClockedNetworkValue>();

    protected MDGameClock GameClock;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GameClock = this.GetGameClock();
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Force));
        this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoinedEvent;
        GameClock.OnGameTick += OnGameTick;
    }

    public override void _ExitTree()
    {
        this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoinedEvent;
        GameClock.OnGameTick -= OnGameTick;
    }

    public void OnGameTick(uint Tick)
    {
        OnChangeAndOneShotValues.ForEach((val) => val.CheckForValueUpdate());
        ReliableIntervalValues.ForEach((val) => val.CheckForValueUpdate());
        UnreliableIntervalValues.ForEach((val) => val.CheckForValueUpdate());
    }

    protected void OnPlayerJoinedEvent(int PeerId)
    {
        if (!IsNetworkMaster() || PeerId == MDStatics.GetPeerId())
        {
            return;
        }
        Timer timer = this.CreateTimer("Synch" + PeerId, true, 0.05f, true, this, nameof(OnJoinTimerTimeout), PeerId);
        timer.Start();
    }

    protected void OnJoinTimerTimeout(Timer timer, int PeerId)
    {
        timer.RemoveAndFree();
        OnChangeAndOneShotValues.ForEach((value) => SendRpcId(PeerId, nameof(ValueChanged), value.GetReliability(), GameClock.GetTick(),
                    OnChangeAndOneShotValues.IndexOf(value), value.GetValueAsString()));
    }

    public override void _Process(float delta)
    {
        if (!MDStatics.IsNetworkActive() || !IsNetworkMaster())
        {
            SetProcess(false);
            return;
        }
        _updateCooldown -= delta;
        if (_updateCooldown <= 0f)
        {
            _updateCooldown = UpdateInterval;
            DoIntervalUpdate();
        }
    }

    ///<summary>Adds a value we will track</summary>
    public void AddValue(IMDClockedNetworkValue value)
    {
        switch (value.GetMode())
        {
            case ClockedPropertyMode.INTERVAL:
                if (value.GetReliability() == MDReliability.Reliable)
                {
                    ReliableIntervalValues.Add(value);
                }
                else
                {
                    UnreliableIntervalValues.Add(value);
                }
                break;
            case ClockedPropertyMode.ON_CHANGE:
            case ClockedPropertyMode.ONE_SHOT:
                OnChangeAndOneShotValues.Add(value);
                value.SubscribeToChangedEvent(ValueChanged);
                break;
        }
    }

    ///<summary>Callback from IMDClockedNetworkValue that is called when one is changed</summary>
    protected void ValueChanged(IMDClockedNetworkValue value)
    {
        if (MDStatics.IsNetworkActive() && IsNetworkMaster())
        {
            SendRpc(nameof(ValueChanged), value.GetReliability(), GameClock.GetTick(),
                    OnChangeAndOneShotValues.IndexOf(value), value.GetValueAsString());
        }
    }

    ///<summary>Sends Rpc in the correct reliability</summary>
    protected void SendRpc(String methodName, MDReliability reliability, params object[] args)
    {
        if (reliability == MDReliability.Reliable)
        {
            Rpc(methodName, args);
        }
        else
        {
            RpcUnreliable(methodName, args);
        }
    }

    protected void SendRpcId(int PeerId, String methodName, MDReliability reliability, params object[] args)
    {
        if (reliability == MDReliability.Reliable)
        {
            RpcId(PeerId, methodName, args);
        }
        else
        {
            RpcUnreliableId(PeerId, methodName, args);
        }
    }

    ///<summary>Sets how often we automatically update</summary>
    public void SetUpdateInterval(float interval)
    {
        UpdateInterval = interval;
    }

    ///<summary>Do interval update for both reliable and unreliable values</summary>
    protected virtual void DoIntervalUpdate()
    {
        List<String> arguments = new List<string>();
        if (UnreliableIntervalValues.Count > 0)
        {
            UnreliableIntervalValues.ForEach((item) => arguments.Add(item.GetValueAsString()));
            SendRpc(nameof(IntervalUpdate), MDReliability.Unreliable, GameClock.GetTick(), MDReliability.Unreliable, arguments);
        }

        if (ReliableIntervalValues.Count > 0)
        {
            arguments.Clear();
            ReliableIntervalValues.ForEach((item) => arguments.Add(item.GetValueAsString()));
            SendRpc(nameof(IntervalUpdate), MDReliability.Reliable, GameClock.GetTick(), MDReliability.Reliable, arguments);
        }
    }

    [Puppet]
    protected void ValueChanged(uint tick, int index, String value)
    {
        OnChangeAndOneShotValues[index].SetValueFromString(value, tick);
    }

    [Puppet]
    protected void IntervalUpdate(uint tick, MDReliability mode, params string[] args)
    {
        if (mode == MDReliability.Reliable)
        {
            for (int i=0; i < ReliableIntervalValues.Count; i++)
            {
                ReliableIntervalValues[i].SetValueFromString(args[i], tick);
            }
        }
        else
        {
            for (int i=0; i < UnreliableIntervalValues.Count; i++)
            {
                UnreliableIntervalValues[i].SetValueFromString(args[i], tick);
            }
        }
    }
}
