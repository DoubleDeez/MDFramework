using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public class MDCRMInterpolatedVector2 : MDClockedReplicatedMember
{
    protected KeyValuePair<uint, Vector2> LastClockedValue = new KeyValuePair<uint, Vector2>(0, Vector2.Zero);

    public MDCRMInterpolatedVector2(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef, MDReplicatedSetting[] Settings) 
                                    : base(Member, Reliable, ReplicatedType, NodeRef, Settings) 
    {
        
    }

    public override void CheckForValueUpdate()
    {
        if (ShouldReplicate())
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
        if (LastClockedValue.Key == 0)
        {
            if (GetValue() != ValueList[NextValue])
            {
                UpdateValue((Vector2)ValueList[NextValue]);
            }
            return;
        }

        // Interpolate between last and current
        float TicksSinceLastValue = CurrentTick - LastClockedValue.Key;
        float TicksBetweenUpdates = NextValue - LastClockedValue.Key;

        // Set the value
        UpdateValue(LastClockedValue.Value.LinearInterpolate((Vector2)ValueList[NextValue], TicksSinceLastValue / TicksBetweenUpdates));
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
            LastClockedValue = new KeyValuePair<uint, Vector2>(key, (Vector2)ValueList[key]);
        }

        // Remove old
        if (oldKeys.Count > 0)
        {
            oldKeys.ForEach((k) => ValueList.Remove(k));
        }

        return foundKey;
    }
}
