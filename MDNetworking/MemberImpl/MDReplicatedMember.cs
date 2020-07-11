using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MD
{
    public class MDReplicatedMember
    {
        public enum Settings
        {
            OnValueChangedEvent,
            Converter
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
        protected MDGameClock GameClock;
        protected IMDDataConverter DataConverter = null;
        protected MethodInfo OnValueChangedCallback = null;

        public MDReplicatedMember(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef,
            MDReplicatedSetting[] Settings)
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            GameSession = MDStatics.GetGameSession();
            Replicator = GameSession.Replicator;
            GameClock = GameSession.GetGameClock();

            this.Member = Member;
            this.Reliable = Reliable;
            this.ReplicatedType = ReplicatedType;
            this.NodeRef = NodeRef;
            ParseSettings(MDReplicator.ParseParameters(typeof(Settings), Settings));
            CheckIfShouldReplicate();
        }

        protected void ParseSettings(MDReplicatedSetting[] SettingsValues)
        {
            foreach (MDReplicatedSetting setting in SettingsValues)
            {
                switch ((Settings) setting.Key)
                {
                    case Settings.OnValueChangedEvent:
                        Node Node = NodeRef.GetRef() as Node;
                        OnValueChangedCallback = Node.GetType().GetMethod(setting.Value.ToString(),
                            BindingFlags.Public | BindingFlags.NonPublic | 
                            BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                        break;
                    case Settings.Converter:
                        Type DataConverterType = Type.GetType(setting.Value.ToString());
                        if (DataConverterType != null && DataConverterType.IsAssignableFrom(typeof(IMDDataConverter)))
                        {
                            DataConverter = Activator.CreateInstance(DataConverterType) as IMDDataConverter;
                        }
                        break;
                }
            }

            // We got no data converter setting
            if (DataConverter == null)
            {
                String NameSpace = Member.GetUnderlyingType().Namespace;
                if (Member.GetUnderlyingType().GetInterface(nameof(IMDDataConverter)) != null)
                {
                    DataConverter = Activator.CreateInstance(Member.GetUnderlyingType()) as IMDDataConverter;
                }
                else if (NameSpace == null || (NameSpace != "System" && NameSpace != "Godot"
                        && NameSpace.StartsWith("Godot.") == false && NameSpace.StartsWith("System.") == false))
                {
                    // Custom class converter
                    Type constructedType = typeof(MDCustomClassDataConverter<>).MakeGenericType(Member.GetUnderlyingType());
                    DataConverter = (IMDDataConverter)Activator.CreateInstance(constructedType);
                }
                else
                {
                    // Set our default converter
                    DataConverter = new MDObjectDataConverter();
                }
            }
        }

        ///<summary>Provides a unique key to identify this member</summary>
        public string GetUniqueKey()
        {
            Node Node = NodeRef.GetRef() as Node;
            return Node?.GetPath() + "#" + Member.Name;
        }

        ///<summary>It is better to implement ReplicateToAll and ReplicateToPeer instead of this method</summary>
        public virtual void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
        {
            Node Instance = NodeRef.GetRef() as Node;
            object CurrentValue = Member.GetValue(Instance);

            if (GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime ||
                GetReplicatedType() == MDReplicatedType.OnChange && DataConverter.ShouldObjectBeReplicated(LastValue, CurrentValue))
            {
                ReplicateToAll(Instance, CurrentValue);
            }
            else if (JoinInProgressPeerId != -1)
            {
                ReplicateToPeer(Instance, CurrentValue, JoinInProgressPeerId);
            }
        }

        ///<summary>Replicate this value to all clients</summary>
        protected virtual void ReplicateToAll(Node Node, object Value)
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

        ///<summary>Replicate this value to the given peer</summary>
        protected virtual void ReplicateToPeer(Node Node, object Value, int PeerId)
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

        public virtual void SetValues(uint Tick, params object[] Parameters)
        {
            // If we got no GameClock or the tick this update is for is past the current tick
            if (GameClock == null || GameClock.GetRemoteTick() >= Tick)
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

        public virtual void CheckForValueUpdate()
        {
            // Check if we are the owner of this
            if (ShouldReplicate() || GameClock == null)
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

        protected virtual void UpdateValue(params object[] Parameters)
        {
            Node Instance = NodeRef.GetRef() as Node;
            object CurrentValue = Member.GetValue(Instance);
            object value = ConvertFromObject(CurrentValue, Parameters);
            Member.SetValue(Instance, value);
            LastValue = value;
            if (OnValueChangedCallback != null)
            {
                OnValueChangedCallback.Invoke(Instance, null);
            }
        }

        protected uint GetGameTick()
        {
            if (GameClock != null)
            {
                return GameClock.GetTick();
            }

            return 0;
        }

        public virtual bool ShouldReplicate()
        {
            return IsShouldReplicate;
        }

        public void CheckIfShouldReplicate()
        {
            MasterAttribute MasterAtr = Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
            MasterSyncAttribute MasterSyncAtr =
                Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
            Node Node = NodeRef.GetRef() as Node;
            bool IsMaster = MDStatics.GetPeerId() == Node.GetNetworkMaster();
            if (!(IsMaster && MasterAtr == null && MasterSyncAtr == null) &&
                !(IsMaster == false && (MasterAtr != null || MasterSyncAtr != null)))
            {
                IsShouldReplicate = false;
            }
            else
            {
                IsShouldReplicate = true;
            }
        }

        public virtual MDReplicatedType GetReplicatedType()
        {
            return ReplicatedType;
        }

        public virtual bool IsReliable()
        {
            return Reliable;
        }

        // Just for convenience
        protected object[] ConvertToObject(object Item, bool Complete)
        {
            return DataConverter.ConvertForSending(Item, Complete);
        }
        
        // Just for convenience
        protected object ConvertFromObject(object CurrentObject, object[] Parameters)
        {
            return DataConverter.CovertBackToObject(CurrentObject, (object[])Parameters);
        }
    }
}