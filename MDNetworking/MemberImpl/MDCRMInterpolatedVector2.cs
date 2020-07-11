using Godot;
using System.Collections.Generic;
using System.Reflection;

namespace MD
{
    public class MDCRMInterpolatedVector2 : MDReplicatedMember
    {
        protected KeyValuePair<uint, Vector2> LastClockedValue = new KeyValuePair<uint, Vector2>(0, Vector2.Zero);
        protected uint LastTickValueWasChanged = 0;

        public MDCRMInterpolatedVector2(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType,
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
                Vector2 Val = GetValueForTick(NextValue);
                Node node = NodeRef.GetRef() as Node;
                if ((Vector2)Member.GetValue(node) != Val)
                {
                    UpdateValue(Val);
                }

                return;
            }

            // Interpolate between last and current
            float TicksSinceLastValue = CurrentTick - LastClockedValue.Key;
            float TicksBetweenUpdates = NextValue - LastClockedValue.Key;

            // Set the value
            Vector2 Value = GetValueForTick(NextValue);
            UpdateValue(LastClockedValue.Value.LinearInterpolate(Value,
                TicksSinceLastValue / TicksBetweenUpdates));
            LastTickValueWasChanged = GameClock.GetTick();
        }

        protected Vector2 GetValueForTick(uint Tick)
        {
            return (Vector2)ConvertFromObject(null, (object[])ValueList[Tick][0]);
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
                LastClockedValue = new KeyValuePair<uint, Vector2>(key, GetValueForTick(key));
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