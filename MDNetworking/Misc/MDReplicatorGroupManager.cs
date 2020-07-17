using System;
using System.Collections.Generic;
using System.Linq;

namespace MD
{
    /// <summary>
    /// Manages replication groups
    /// </summary>
    public class MDReplicatorGroupManager
    {
        protected Dictionary<string, int> GroupNameToNumberMap = new Dictionary<string, int>();

        protected List<HashSet<MDReplicatedMember>> ReplicationGroups = new List<HashSet<MDReplicatedMember>>();

        int CurrentReplicationGroup = -1;

        public MDReplicatorGroupManager(int TotalGroups)
        {
            for (int i = 0; i < TotalGroups; i++)
            {
                ReplicationGroups.Add(new HashSet<MDReplicatedMember>());
            }
        }

        /// <summary>
        /// Get the members to replicate for the current frame
        /// </summary>
        /// <returns>List of members to replicate</returns>
        public HashSet<MDReplicatedMember> GetMembersToReplicate()
        {
            CurrentReplicationGroup++;
            if (CurrentReplicationGroup >= ReplicationGroups.Count)
            {
                CurrentReplicationGroup = 0;
            }

            return ReplicationGroups[CurrentReplicationGroup];
        }

        /// <summary>
        /// Adds a replicated member to the group manager
        /// </summary>
        /// <param name="Member">The member to add</param>
        public void AddReplicatedMember(MDReplicatedMember Member)
        {
            if (Member.GetReplicatedType() != MDReplicatedType.Interval)
            {
                return;
            }

            int Group = -1;

            if (Member.ReplicationGroup != null)
            {
                Group = GetReplicationGroupByName(Member.ReplicationGroup);
            }
            else
            {
                Group = GetGroupWithLeastMembers();
            }

            ReplicationGroups[Group].Add(Member);
        }

        /// <summary>
        /// Removes the replicated member from any groups it is in
        /// </summary>
        /// <param name="Member">The member to remove</param>
        public void RemoveReplicatedMember(MDReplicatedMember Member)
        {
            if (Member.GetReplicatedType() != MDReplicatedType.Interval)
            {
                return;
            }

            foreach (HashSet<MDReplicatedMember> group in ReplicationGroups)
            {
                if (group.Contains(Member))
                {
                    group.Remove(Member);
                    break;
                }
            }
        }

        /// <summary>
        /// Get the replication group by name
        /// </summary>
        /// <param name="Name">The name of the replication group</param>
        /// <returns>The group index</returns>
        protected int GetReplicationGroupByName(string Name)
        {
            if (!GroupNameToNumberMap.ContainsKey(Name))
            {
                GroupNameToNumberMap.Add(Name, GetGroupWithLeastMembers());
            }

            return GroupNameToNumberMap[Name];
        }

        /// <summary>
        /// Find the group with the least members
        /// </summary>
        /// <returns>The index of the group with the least members</returns>
        protected int GetGroupWithLeastMembers()
        {
            int GroupWithLeastMembers = 0;
            for (int i = 1; i < ReplicationGroups.Count; i++)
            {
                if (ReplicationGroups[i].Count < ReplicationGroups[GroupWithLeastMembers].Count)
                {
                    GroupWithLeastMembers = i;
                }
            }

            return GroupWithLeastMembers;
        }
    }
}