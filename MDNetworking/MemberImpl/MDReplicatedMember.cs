using Godot;
using System;
using System.Reflection;

namespace MD
{
    public class MDReplicatedMember
    {
        public bool ProcessWhilePaused { get; set; } = true;

        public string ReplicationGroup { get; set; } = null;

        protected const string LOG_CAT = "LogReplicatedMember";

        protected MemberInfo Member;

        protected object LastValue;

        protected bool Reliable;

        protected MDReplicatedType ReplicatedType;

        protected WeakRef NodeRef;

        protected bool IsShouldReplicate = false;

        public MDReplicatedMember(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef,
            MDReplicatedSetting[] Settings)
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            this.Member = Member;
            this.Reliable = Reliable;
            this.ReplicatedType = ReplicatedType;
            this.NodeRef = NodeRef;
            CheckIfShouldReplicate();
        }

        ///<summary>Provides a unique key to identify this member</summary>
        public string GetUniqueKey()
        {
            Node Node = NodeRef.GetRef() as Node;
            return Node?.GetPath() + "#" + Member.Name;
        }

        protected virtual object GetValue()
        {
            Node Instance = NodeRef.GetRef() as Node;
            FieldInfo Field = Member as FieldInfo;

            if (Field != null)
            {
                return Field.GetValue(Instance);
            }

            PropertyInfo Property = Member as PropertyInfo;
            return Property != null ? Property.GetValue(Instance) : null;
        }

        ///<summary>It is better to implement ReplicateToAll and ReplicateToPeer instead of this method</summary>
        public virtual void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
        {
            object CurrentValue = GetValue();
            Node Instance = NodeRef.GetRef() as Node;

            if (GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime ||
                GetReplicatedType() == MDReplicatedType.OnChange && Equals(LastValue, CurrentValue) == false)
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
                Node.Rset(Member.Name, Value);
            }
            else
            {
                Node.RsetUnreliable(Member.Name, Value);
            }

            LastValue = Value;
        }

        ///<summary>Replicate this value to the given peer</summary>
        protected virtual void ReplicateToPeer(Node Node, object Value, int PeerId)
        {
            MDLog.Debug(LOG_CAT, $"Replicating to JIP Peer {PeerId} for member {Member.Name} with value {Value}");
            if (IsReliable())
            {
                Node.RsetId(PeerId, Member.Name, Value);
            }
            else
            {
                Node.RsetUnreliableId(PeerId, Member.Name, Value);
            }
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

        public virtual void SetValue(uint Tick, object Value)
        {
            // This is just here to avoid casting so much in the replicator
            // Used by Clocked values
        }

        public virtual void SetValues(uint Tick, params object[] Parameters)
        {
            // This is just here to avoid casting so much in the replicator
            // Used by Clocked values
        }

        public virtual void CheckForValueUpdate()
        {
            // This is just here to avoid casting so much in the replicator
            // Used by Clocked values
        }
    }
}