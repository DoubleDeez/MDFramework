using Godot;
using System.Collections.Generic;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Clocked interpolated value
    /// </summary>
    public abstract class MDCRMInterpolatedValue<T> : MDReplicatedMember where T : new()
    {
        protected KeyValuePair<uint, T> LastClockedValue = new KeyValuePair<uint, T>(0, new T());
        protected uint LastTickValueWasChanged = 0;

        public MDCRMInterpolatedValue(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType,
            WeakRef NodeRef, MDReplicatedSetting[] Settings)
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
                T Val = GetValueForTick(NextValue);
                Node node = NodeRef.GetRef() as Node;
                if (HasValueChanged((T)Member.GetValue(node), Val))
                {
                    UpdateValue(Val);
                }

                return;
            }

            // Interpolate between last and current
            float TicksSinceLastValue = CurrentTick - LastClockedValue.Key;
            float TicksBetweenUpdates = NextValue - LastClockedValue.Key;

            // Set the value
            T Value = GetValueForTick(NextValue);
            float Alpha = Mathf.Clamp(TicksSinceLastValue / TicksBetweenUpdates, 0f, 1f);
            UpdateValue(LinearInterpolate(LastClockedValue.Value, Value, Alpha));
            LastTickValueWasChanged = GameClock.GetRemoteTick();
        }

        protected abstract T LinearInterpolate(T LastValue, T Value, float Alpha);
        protected abstract bool HasValueChanged(T CurValue, T NewValue);

        private T GetValueForTick(uint Tick)
        {
            return (T)ConvertFromObject(null, (object[])ValueList[Tick][0]);
        }

        // Finds the next value that is in the future and removes old values from the list
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
                LastClockedValue = new KeyValuePair<uint, T>(key, GetValueForTick(key));
            }

            // Remove old
            if (oldKeys.Count > 0)
            {
                oldKeys.ForEach(k => ValueList.Remove(k));
            }

            return foundKey;
        }
    }
}