using Godot;
using System;
using System.Collections.Generic;

public enum ClockedPropertyMode 
{
    INTERVAL,       // Will update along with the normal update interval
    ON_CHANGE,      // Will only update on change and will retain the value until it is changed again
    ONE_SHOT        // Will only return the value only on the tick it was set or the next tick it is checked after
}

public delegate void OnValueChangedEventHandler(IMDClockedNetworkValue value);

public interface IMDClockedNetworkValue
{

    String GetValueAsString();

    void SetValueFromString(String value, uint tick);

    ClockedPropertyMode GetMode();

    MDReliability GetReliability();

    void SubscribeToChangedEvent(OnValueChangedEventHandler handler);

    void CheckForValueUpdate();
}

public class MDClockedNetworkValue<T> : IMDClockedNetworkValue
{
    public delegate void OnChangeHandler(T newValue, T oldValue);

    ///<summary>Event triggers every time this value is changed across the network</summary>
    public event OnChangeHandler OnValueChangedEvent = delegate {};

    public event OnValueChangedEventHandler OnChangedEventSimple = delegate {};

    protected T Value;

    protected ClockedPropertyMode Mode;
    
    protected MDReliability Reliability;

    protected SortedDictionary<uint, T> ValueList = new SortedDictionary<uint, T>();

    protected MDGameClock GameClock;

    protected bool IsMaster = false;

    protected uint LastTickValueWasChanged = 0;

    public MDClockedNetworkValue(T initialValue, bool isMaster, ClockedPropertyMode mode = ClockedPropertyMode.INTERVAL,
                                 MDReliability reliability = MDReliability.Unreliable)
    {
        Value = initialValue;
        IsMaster = isMaster;
        Mode = mode;
        Reliability = reliability;
        GameClock = MDStatics.GetGameSynchronizer().GameClock;
    }

    public virtual T GetValue()
    {
        CheckForValueUpdate();
        return Value;
    }

    public virtual void SetValue(T value)
    {
        T oldValue = Value;
        Value = value;
        OnValueChangedEvent(value, oldValue);
        OnChangedEventSimple(this);
    }

    public virtual String GetValueAsString()
    {
        // Could be overwritten for a more optional conversion
        return GD.Var2Str(Value);
    }

    public virtual void SetValueFromString(String value, uint tick)
    {
        // Could be overwritten for a more optional conversion
        if (!ValueList.ContainsKey(tick))
        {
            ValueList.Add(tick, (T)GD.Str2Var(value));
            CheckForValueUpdate();
        }
    }

    public virtual void CheckForValueUpdate()
    {
        if (IsMaster)
        {
            return;
        }
        
        // Find the most recent update
        List<uint> touchedKeys = new List<uint>();
        uint foundKey = 0;
        foreach (uint key in ValueList.Keys)
        {
            if (key > GameClock.GetRemoteTick())
            {
                break;
            }
            touchedKeys.Add(key);
            foundKey = key;
        }

        // Didn't find any updates
        if (foundKey == 0)
        {
            // One shot only returns the value on the same tick it is set
            if (Mode == ClockedPropertyMode.ONE_SHOT && LastTickValueWasChanged != GameClock.GetTick())
            {
                SetValue(default(T));
            }
            return;
        }

        // Set the value
        SetValue(ValueList[foundKey]);
        LastTickValueWasChanged = GameClock.GetRemoteTick();

        // Remove old
        touchedKeys.ForEach((k) => ValueList.Remove(k));
    }

    public virtual ClockedPropertyMode GetMode()
    {
        return Mode;
    }

    public MDReliability GetReliability()
    {
        return Reliability;
    }

    public void SubscribeToChangedEvent(OnValueChangedEventHandler handler)
    {
        OnChangedEventSimple += handler;
    }
}
