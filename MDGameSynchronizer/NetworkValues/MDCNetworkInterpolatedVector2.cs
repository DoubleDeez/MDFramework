using Godot;
using System;
using System.Collections.Generic;

///<summary>Simple implementation for a networked and interpolated vector2 value</summary>
public class MDCNetworkInterpolatedVector2 : MDClockedNetworkValue<Vector2>
{
    private static readonly string LOG_CAT = "LogClockInterpolatedVector2";

    protected KeyValuePair<uint, Vector2> LastValue = new KeyValuePair<uint, Vector2>(0, Vector2.Zero);

    public MDCNetworkInterpolatedVector2(Vector2 initialValue, bool isMaster) : base(initialValue, isMaster)
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Force));
    }

    public MDCNetworkInterpolatedVector2(Vector2 initialValue, bool isMaster, ClockedPropertyMode mode = ClockedPropertyMode.INTERVAL,
                                 MDReliability reliability = MDReliability.Unreliable) : base(initialValue, isMaster, mode, reliability)
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Force));
    }

    public override void CheckForValueUpdate()
    {
        if (IsMaster)
        {
            return;
        }

        // Find the most recent update
        uint CurrentTick = GameClock.GetRemoteTick();
        if (ValueList.Count == 0 || CurrentTick == LastTickValueWasChanged)
        {
            return;
        }

        uint NextValue = FindNextValue();
        if (NextValue == 0)
        {
            // We got no more values in queue
            return;
        }

        // Nothing to interpolate yet
        if (LastValue.Key == 0)
        {
            if (Value != ValueList[NextValue])
            {
                SetValue(ValueList[NextValue]);
            }
            return;
        }

        // Interpolate between last and current
        float TicksSinceLastValue = CurrentTick - LastValue.Key;
        float TicksBetweenUpdates = NextValue - LastValue.Key;

        // Set the value
        SetValue(LastValue.Value.LinearInterpolate(ValueList[NextValue], TicksSinceLastValue / TicksBetweenUpdates));
        LastTickValueWasChanged = GameClock.GetTick();
    }

    ///<summary>Finds the next value that is in the future and removes old values from the list</summary>
    private uint FindNextValue()
    {
        List<uint> oldKeys = new List<uint>();
        uint foundKey = 0;
        uint CurrentTick = GameClock.GetRemoteTick();

        // Find the next value
        foreach (uint key in ValueList.Keys)
        {
            if (key > CurrentTick)
            {
                foundKey = key;
                break;
            }
            oldKeys.Add(key);
            LastValue = new KeyValuePair<uint, Vector2>(key, ValueList[key]);
        }

        // Remove old
        if (oldKeys.Count > 0)
        {
            oldKeys.ForEach((k) => ValueList.Remove(k));
        }

        return foundKey;
    } 
}
