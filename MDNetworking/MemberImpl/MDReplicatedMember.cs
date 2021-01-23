using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// Our base class for replicated members
    /// </summary>
    public class MDReplicatedMember
    {
        public enum Settings
        {
            OnValueChangedEvent,
            Converter,
            CallOnValueChangedEventLocally
        }

        protected const string LOG_CAT = "LogReplicatedMember";

        public bool ProcessWhilePaused { get; set; } = true;
        public string ReplicationGroup { get; set; } = null;
        protected SortedDictionary<uint, List<object>> ValueList = new SortedDictionary<uint, List<object>>();
        protected MemberInfo Member;
        protected object LastValue;
        protected bool Reliable;
        protected MDReplicatedType ReplicatedType;
        protected WeakRef NodeRef;
        protected bool IsShouldReplicate = false;
        protected MDReplicator Replicator;
        protected MDGameSession GameSession;
        protected MDGameSynchronizer GameSynchronizer;
        protected MDGameClock GameClock;
        protected IMDDataConverter DataConverter = null;
        protected MethodInfo OnValueChangedMethodCallback = null;
        protected EventInfo OnValueChangedEventCallback = null;

        protected bool ShouldCallOnValueChangedCallbackLocally = false;

        public MDReplicatedMember(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef,
            MDReplicatedSetting[] Settings)
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            GameSession = MDStatics.GetGameSession();
            Replicator = GameSession.Replicator;
            GameClock = GameSession.GetGameClock();
            GameSynchronizer = GameSession.GetGameSynchronizer();

            this.Member = Member;
            this.Reliable = Reliable;
            this.ReplicatedType = ReplicatedType;
            this.NodeRef = NodeRef;
            ParseSettings(MDReplicator.ParseParameters(typeof(Settings), Settings));
            CheckIfShouldReplicate();
        }

        /// <summary>
        /// Parses the settings we know about
        /// </summary>
        /// <param name="SettingsValues">A setting array that has been run through MDReplicator.ParseParameters</param>
        protected void ParseSettings(MDReplicatedSetting[] SettingsValues)
        {
            foreach (MDReplicatedSetting setting in SettingsValues)
            {
                switch ((Settings) setting.Key)
                {
                    case Settings.OnValueChangedEvent:
                        Node Node = NodeRef.GetRef() as Node;
                        OnValueChangedMethodCallback = Node.GetType().GetMethodRecursive(setting.Value.ToString());
                        if (OnValueChangedMethodCallback == null)
                        {
                            OnValueChangedEventCallback = Node.GetType().GetEvent(setting.Value.ToString());
                        }
                        MDLog.CError(OnValueChangedMethodCallback == null && OnValueChangedEventCallback == null, LOG_CAT, $"Failed to find method or event with name {setting.Value.ToString()} on Node {Node.GetPath()}");
                        break;
                    case Settings.Converter:
                        Type DataConverterType = Type.GetType(setting.Value.ToString());
                        DataConverter = MDStatics.CreateConverterOfType(DataConverterType);
                        break;
                    case Settings.CallOnValueChangedEventLocally:
                        ShouldCallOnValueChangedCallbackLocally = setting.Value as bool? ?? false;
                        break;
                }
            }

            // We got no data converter, get default one
            if (DataConverter == null)
            {
                DataConverter = MDStatics.GetConverterForType(Member.GetUnderlyingType());
            }
        }

        /// <summary>
        /// Provides a unique key to identify this member
        /// </summary>
        /// <returns>The unique key</returns>
        public string GetUniqueKey()
        {
            Node Node = NodeRef.GetRef() as Node;
            return Node?.GetPath() + "#" + Member.Name;
        }

        /// <summary>
        /// It is better to implement ReplicateToAll and ReplicateToPeer instead of this method
        /// </summary>
        /// <param name="JoinInProgressPeerId">The ID of any peer that is currently joining or -1 if none is</param>
        /// <param name="IsIntervalReplicationTime">True if it is time for us to do interval replication</param>
        public virtual void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
        {
            Node Instance = NodeRef.GetRef() as Node;
            object CurrentValue = Member.GetValue(Instance);

            if (GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime ||
                GetReplicatedType() == MDReplicatedType.OnChange && DataConverter.ShouldObjectBeReplicated(LastValue, CurrentValue))
            {
                ReplicateToAll(CurrentValue);
                CheckCallLocalOnChangeCallback(CurrentValue);
            }
            else if (JoinInProgressPeerId != -1)
            {
                ReplicateToPeer(CurrentValue, JoinInProgressPeerId);
            }
        }

        /// <summary>
        /// Replicate this value to all clients
        /// </summary>
        /// <param name="Value">The value to replicate</param>
        protected virtual void ReplicateToAll(object Value)
        {
            MDLog.Debug(LOG_CAT, $"Replicating {Member.Name} with value {Value} from {LastValue}");
            if (IsReliable())
            {
                Replicator.Rpc(MDReplicator.REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()),
                                GetGameTick(), ConvertToObject(Value, false));
            }
            else
            {
                Replicator.RpcUnreliable(MDReplicator.REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()),
                                GetGameTick(), ConvertToObject(Value, false));
            }

            LastValue = Value;
        }

        /// <summary>
        /// Replicate this value to the given peer
        /// </summary>
        /// <param name="Value">The value to replicate</param>
        /// <param name="PeerId">The peer to replicate to</param>
        protected virtual void ReplicateToPeer(object Value, int PeerId)
        {
            MDLog.Debug(LOG_CAT, $"Replicating to JIP Peer {PeerId} for member {Member.Name} with value {Value}");
            if (IsReliable())
            {
                Replicator.RpcId(PeerId, MDReplicator.REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()),
                                GetGameTick(), ConvertToObject(Value, true));
            }
            else
            {
                Replicator.RpcUnreliableId(PeerId, MDReplicator.REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()),
                                GetGameTick(), ConvertToObject(Value, true));
            }
        }

        /// <summary>
        /// Set the vlaues of this replicated member, this is called on clients 
        /// after we receive an update from the network master of a member.
        /// </summary>
        /// <param name="Tick">The tick this update is for</param>
        /// <param name="Parameters">The value</param>
        public virtual void SetValues(uint Tick, params object[] Parameters)
        {
            // If the tick this update is for is past the current tick
            // Or if we are currently synching
            if (GameClock.GetRemoteTick() >= Tick || IsSynchInProgress())
            {
                UpdateValue(Parameters);
            }
            else
            {
                if (!ValueList.ContainsKey(Tick))
                {
                    ValueList.Add(Tick, new List<object>());
                }

                ValueList[Tick].Add(Parameters);
            }
        }

        /// <summary>
        /// Checks if we got an updated value, this is called on clients that this value is replicated to.
        /// </summary>
        public virtual void CheckForValueUpdate()
        {
            // Check if we are the owner of this
            if (ShouldReplicate())
            {
                return;
            }

            uint RemoteTick = GameClock.GetRemoteTick();
            bool ValueChanged = false;

            // Find the most recent update
            List<uint> touchedKeys = new List<uint>();
            foreach (uint key in ValueList.Keys)
            {
                if (key > RemoteTick)
                {
                    break;
                }

                ValueList[key].ForEach(parameters => UpdateValue((object[])parameters));
                ValueChanged = true;
                touchedKeys.Add(key);
            }

            if (ValueChanged)
            {
                // Remove old
                touchedKeys.ForEach(k => ValueList.Remove(k));
            }
        }

        /// <summary>
        /// Update the value by setting it on the node
        /// </summary>
        /// <param name="Parameters">The values to update with</param>
        protected virtual void UpdateValue(params object[] Parameters)
        {
            Node Instance = NodeRef.GetRef() as Node;
            object CurrentValue = Member.GetValue(Instance);
            object value = ConvertFromObject(CurrentValue, Parameters);
            Member.SetValue(Instance, value);
            LastValue = value;
            CallOnChangeCallback(LastValue);
        }

        /// <summary>
        /// Get the current game tick
        /// </summary>
        /// <returns>The current tick or 0 if game clock is not active</returns>
        protected uint GetGameTick()
        {
            return GameClock.GetTick();
        }

        /// <summary>
        /// Tells us if we should replicate this to other clients
        /// </summary>
        /// <returns>True if we should, false if not</returns>
        public virtual bool ShouldReplicate()
        {
            return IsShouldReplicate;
        }

        /// <summary>
        /// Checks if we should replicate this to other clients and sets our internal state accordingly.
        /// </summary>
        public void CheckIfShouldReplicate()
        {
            MasterAttribute MasterAtr = MDReflectionCache.GetCustomAttribute<MasterAttribute>(Member) as MasterAttribute;
            MasterSyncAttribute MasterSyncAtr = MDReflectionCache.GetCustomAttribute<MasterSyncAttribute>(Member) as MasterSyncAttribute;
            Node Node = NodeRef.GetRef() as Node;
            bool IsMaster = MDStatics.GetPeerId() == Node.GetNetworkMaster();
            IsShouldReplicate = (IsMaster && MasterAtr == null && MasterSyncAtr == null) || (IsMaster == false && (MasterAtr != null || MasterSyncAtr != null));
        }

        /// <summary>
        /// Get our replication type
        /// </summary>
        /// <returns>The replication type</returns>
        public virtual MDReplicatedType GetReplicatedType()
        {
            return ReplicatedType;
        }

        /// <summary>
        /// Are we sending rpc as reliable
        /// </summary>
        /// <returns>True if we are, false if not</returns>
        public virtual bool IsReliable()
        {
            return Reliable;
        }

        /// <summary>
        /// Just for convenience
        /// </summary>
        /// <param name="Item">The item to convert</param>
        /// <param name="Complete">Should we do a complete conversion?</param>
        /// <returns>The object array to send</returns>
        protected object[] ConvertToObject(object Item, bool Complete)
        {
            return DataConverter.ConvertForSending(Item, Complete);
        }
        
        /// <summary>
        /// Just for convenience
        /// </summary>
        /// <param name="CurrentObject">The current object we are updating</param>
        /// <param name="Parameters">The values we got across the network</param>
        /// <returns>The updated / new value</returns>
        protected object ConvertFromObject(object CurrentObject, object[] Parameters)
        {
            return DataConverter.ConvertBackToObject(CurrentObject, (object[])Parameters);
        }

        /// <summary>
        /// Returns true if we are currently being synched
        /// </summary>
        /// <returns>True if we are being synched, false if not</returns>
        protected bool IsSynchInProgress()
        {
            if (GameSynchronizer.SynchronizationState == MDGameSynchronizer.SynchronizationStates.SYNCRHONIZED)
            {
                return false;
            } 

            return true;
        }

        protected void CheckCallLocalOnChangeCallback(object Value = null)
        {
            if (ShouldCallOnValueChangedCallbackLocally)
            {
                CallOnChangeCallback(Value);
            }
        }

        protected void CallOnChangeCallback(object Value = null)
        {
            MethodInfo CallbackMethod = OnValueChangedMethodCallback ?? OnValueChangedEventCallback?.GetRaiseMethod(true);
            if (CallbackMethod != null)
            {
                ParameterInfo[] Params = CallbackMethod.GetParameters();
                Node Instance = NodeRef.GetRef() as Node;
                if (Params.Length > 0 && Value != null)
                {
                    CallbackMethod.Invoke(Instance, new [] { Value });
                }
                else
                {
                    CallbackMethod.Invoke(Instance, null);
                }
            }
        }
    }
}