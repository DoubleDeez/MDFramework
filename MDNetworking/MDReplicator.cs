using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public class MDReplicator
{
    private List<ReplicatedNode> NodeList = new List<ReplicatedNode>();
    private const string LOG_CAT = "LogReplicator";

    public MDReplicator()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
    }

    // TODO - queue new joiner for replication

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        List<MemberInfo> Members = new List<MemberInfo>();
        Type NodeType = Instance.GetType();
        while (NodeType != typeof(Node))
        {
            Members.AddRange(NodeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Members.AddRange(NodeType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            NodeType = NodeType.BaseType;
        }

        List<ReplicatedMember> NodeMembers = new List<ReplicatedMember>();
        foreach(MemberInfo Member in Members)
        {
            bool AlreadyExists = false;
            foreach (ReplicatedMember NodeMember in NodeMembers)
            {
                if (NodeMember.Member.DeclaringType == Member.DeclaringType && NodeMember.Member.Name == Member.Name)
                {
                    AlreadyExists = true;
                    break;
                }
            }

            if (AlreadyExists)
            {
                continue;
            }

            MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                ReplicatedMember NodeMember = new ReplicatedMember();
                NodeMember.Member = Member;
                NodeMember.IsReliable = RepAttribute.Reliability == MDReliability.Reliable;
                NodeMembers.Add(NodeMember);

                if (HasRPCModeSet(Member) == false)
                {
                    Instance.RsetConfig(Member.Name, MultiplayerAPI.RPCMode.Puppet);
                }
            }
        }

        if (NodeMembers.Count > 0)
        {
            NodeList.Add(new ReplicatedNode(Instance, NodeMembers));
        }
    }

    // Unregisters the given instance's fields marked with [MDReplicated()]
    public void UnregisterReplication(Node Instance)
    {
        if (Godot.Object.IsInstanceValid(Instance))
        {
            NodeList.RemoveAll(RepNode => RepNode.Instance.GetRef() == Instance);
        }
    }

    // Broadcasts out replicated modified variables if we're the server, propagates changes recieved from the server if client.
    public void TickReplication()
    {
        #if DEBUG
        using (MDProfiler Profiler = new MDProfiler("MDReplicator.TickReplication"))
        #endif
        {
            if (MDStatics.IsNetworkActive() == false)
            {
                return;
            }

            for (int i = NodeList.Count - 1; i >= 0; --i)
            {

                ReplicatedNode RepNode = NodeList[i];
                Node Instance = RepNode.Instance.GetRef() as Node;
                if (Godot.Object.IsInstanceValid(Instance))
                {
                    for (int j = 0; j < RepNode.Members.Count; ++j)
                    {
                        ReplicatedMember RepMember = RepNode.Members[j];
                        MasterAttribute MasterAtr = RepMember.Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
                        MasterSyncAttribute MasterSyncAtr = RepMember.Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
                        bool IsMaster = MDStatics.GetPeerId() == Instance.GetNetworkMaster();
                        if (!(IsMaster && MasterAtr == null && MasterSyncAtr == null) && !(IsMaster == false && (MasterAtr != null || MasterSyncAtr != null)))
                        {
                            continue;
                        }
                        
                        object CurrentValue = null;
                        FieldInfo Field = RepMember.Member as FieldInfo;
                        if (Field != null)
                        {
                            CurrentValue = Field.GetValue(Instance);
                        }

                        if (CurrentValue == null)
                        {
                            PropertyInfo Property = RepMember.Member as PropertyInfo;
                            if (Property != null)
                            {
                                CurrentValue = Property.GetValue(Instance);
                            }
                        }

                        if (RepMember.LastValue != CurrentValue)
                        {
                            if (RepMember.IsReliable)
                            {
                                Instance.Rset(RepMember.Member.Name, CurrentValue);
                            }
                            else
                            {
                                Instance.RsetUnreliable(RepMember.Member.Name, CurrentValue);
                            }
                            RepMember.LastValue = CurrentValue;
                        }
                    }
                }
                else
                {
                    NodeList.RemoveAt(i);
                }
            }
        }
    }

    private bool HasRPCModeSet(MemberInfo Member)
    {
        MasterAttribute MasterAtr = Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
        MasterSyncAttribute MasterSyncAtr = Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
        PuppetAttribute PuppetAtr = Member.GetCustomAttribute(typeof(PuppetAttribute)) as PuppetAttribute;
        PuppetSyncAttribute PuppetSyncAtr = Member.GetCustomAttribute(typeof(PuppetSyncAttribute)) as PuppetSyncAttribute;
        RemoteAttribute RemoteAtr = Member.GetCustomAttribute(typeof(RemoteAttribute)) as RemoteAttribute;
        RemoteSyncAttribute RemoteSyncAtr = Member.GetCustomAttribute(typeof(RemoteSyncAttribute)) as RemoteSyncAttribute;

        return MasterAtr != null || MasterSyncAtr != null || PuppetAtr != null || PuppetSyncAtr != null || RemoteAtr != null || RemoteSyncAtr != null;
    }
}

class ReplicatedNode
{
    public ReplicatedNode(Node InInstance, List<ReplicatedMember> InMembers)
    {
        Instance = Godot.Object.WeakRef(InInstance);
        Members = InMembers;
    }

    public WeakRef Instance;

    public List<ReplicatedMember> Members;
}

class ReplicatedMember
{
    public MemberInfo Member;

    public object LastValue;

    public bool IsReliable;
}