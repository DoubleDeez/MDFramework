using Godot;
using System;
using System.Collections.Generic;

public class MDReplicatorGroupManager
{
    protected Dictionary<String, int> GroupNameToNumberMap = new Dictionary<string, int>();

    protected List<HashSet<MDReplicatedMember>> ReplicationGroups = new List<HashSet<MDReplicatedMember>>();

    int CurrentReplicationGroup = -1;

    public MDReplicatorGroupManager(int TotalGroups)
    {
        for (int i = 0; i < TotalGroups; i++)
        {
            ReplicationGroups.Add(new HashSet<MDReplicatedMember>());
        }
    }

    public HashSet<MDReplicatedMember> GetMembersToReplicate()
    {
        CurrentReplicationGroup++;
        if (CurrentReplicationGroup >= ReplicationGroups.Count)
        {
            CurrentReplicationGroup = 0;
        }

        return ReplicationGroups[CurrentReplicationGroup];
    }

    ///<summary>Adds a replicated member to the group manager</summary>
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

    ///<Summary>Get the replication group by name</summary>
    protected int GetReplicationGroupByName(String Name)
    {
        if (!GroupNameToNumberMap.ContainsKey(Name))
        {
            GroupNameToNumberMap.Add(Name, GetGroupWithLeastMembers());
        }

        return GroupNameToNumberMap[Name];
    }

    // Find the group with the least members
    protected int GetGroupWithLeastMembers()
    {
        int GroupWithLeastMembers = 0;
        for (int i=1; i < ReplicationGroups.Count; i++)
        {
            if (ReplicationGroups[i].Count < ReplicationGroups[GroupWithLeastMembers].Count)
            {
                GroupWithLeastMembers = i;
            }
        }

        return GroupWithLeastMembers;
    }

}
