using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

struct NewPlayer
{
    public int PeerId;
    public float JoinTime;

    public bool IsReadyForReplication()
    {
        MDGameInstance GameInstance = MDStatics.GetGameSession().GetGameInstance();
        if (GameInstance.UseGameSynchronizer() && 
            GameInstance.GetGameSynchronizer().IsDelayReplicatorUntilAllNodesAreSynched())
        {
            // If synch is active check if peer has completed node synch
            if (GameInstance.GetGameSynchronizer().HasPeerCompletedNodeSynch(PeerId))
            {
                return true;
            }
        }
        // If synch is not active then we use a delay
        else if ((JoinTime + MDReplicator.JIPWaitTime) < OS.GetTicksMsec())
        {
            return true;
        }
        return false;
    }

    public NewPlayer(int PeerId, float JoinTime)
    {
        this.PeerId = PeerId;
        this.JoinTime = JoinTime;
    }
}

[MDAutoRegister]
public class MDReplicator
{
    private List<ReplicatedNode> NodeList = new List<ReplicatedNode>();
    private Queue<NewPlayer> JIPPlayers = new Queue<NewPlayer>();

    private const string LOG_CAT = "LogReplicator";
    public const float JIPWaitTime = 1000f;

    public MDReplicator()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
    }

    public void OnPlayerJoined(int PeerId)
    {
        JIPPlayers.Enqueue(new NewPlayer(PeerId, OS.GetTicksMsec()));
    }

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        List<MemberInfo> Members = MDStatics.GetTypeMemberInfos(Instance);
        List<IReplicatedMember> NodeMembers = new List<IReplicatedMember>();
        foreach(MemberInfo Member in Members)
        {
            MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                ReplicatedMember NodeMember = new ReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable, 
                                                                    RepAttribute.ReplicatedType, WeakRef.WeakRef(Instance));
                NodeMembers.Add(NodeMember);

                MDLog.Trace(LOG_CAT, "Adding Replicated Node {0} Member {1}", Instance.Name, Member.Name);

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

    // Peeks the JIPPlayer queue and returns the first peerid if enough time has passed
    private int CheckForNewPlayer()
    {
        if (JIPPlayers.Count > 0)
        {
            NewPlayer JIPPlayer = JIPPlayers.Peek();
            if (JIPPlayer.IsReadyForReplication())
            {
                MDLog.Debug(LOG_CAT, "JIP Peer Id {0} ready for MDReplicated", JIPPlayer.PeerId);
                return JIPPlayers.Dequeue().PeerId;
            }
        }

        return -1;
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

            int JIPPeerId = CheckForNewPlayer();

            for (int i = NodeList.Count - 1; i >= 0; --i)
            {
                ReplicatedNode RepNode = NodeList[i];
                Node Instance = RepNode.Instance.GetRef() as Node;
                if (Godot.Object.IsInstanceValid(Instance))
                {
                    for (int j = 0; j < RepNode.Members.Count; ++j)
                    {
                        IReplicatedMember RepMember = RepNode.Members[j];
                        if (!RepMember.ShouldReplicate())
                        {
                            continue;
                        }

                        if (RepMember.GetReplicatedType() == MDReplicatedType.JoinInProgress && JIPPeerId == -1)
                        {
                            continue;
                        }
                        
                        RepMember.Replicate(JIPPeerId);
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

interface IReplicatedMember
{
    bool ShouldReplicate();

    void Replicate(int JoinInProgressPeerId);

    MDReplicatedType GetReplicatedType();

    bool IsReliable();
}

class ReplicatedNode
{
    public ReplicatedNode(Node InInstance, List<IReplicatedMember> InMembers)
    {
        Instance = Godot.Object.WeakRef(InInstance);
        Members = InMembers;
    }

    public WeakRef Instance;

    public List<IReplicatedMember> Members;
}

class ReplicatedMember : IReplicatedMember
{
    private const string LOG_CAT = "LogReplicatedMember";

    private MemberInfo Member;

    private object LastValue;

    private bool Reliable;

    private MDReplicatedType ReplicatedType;

    private WeakRef NodeRef;

    public ReplicatedMember(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef)
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        this.Member = Member;
        this.Reliable = Reliable;
        this.ReplicatedType = ReplicatedType;
        this.NodeRef = NodeRef;
    }

    public object GetValue()
    {
        Node Instance = NodeRef.GetRef() as Node;
        FieldInfo Field = Member as FieldInfo;

        if (Field != null)
        {
            return Field.GetValue(Instance);
        }

        PropertyInfo Property = Member as PropertyInfo;
        if (Property != null)
        {
            return Property.GetValue(Instance);
        }

        return null;
    }

    public void Replicate(int JoinInProgressPeerId)
    {
        object CurrentValue = GetValue();
        Node Instance = NodeRef.GetRef() as Node;

        if (ReplicatedType == MDReplicatedType.Always || object.Equals(LastValue, CurrentValue) == false)
        {
            MDLog.Debug(LOG_CAT, "Replicating {0} with value {1} from {2}", Member.Name, CurrentValue, LastValue);
            if (Reliable)
            {
                Instance.Rset(Member.Name, CurrentValue);
            }
            else
            {
                Instance.RsetUnreliable(Member.Name, CurrentValue);
            }
            LastValue = CurrentValue;
        }
        else if (JoinInProgressPeerId != -1)
        {
            MDLog.Debug(LOG_CAT, "Replicating to JIP Peer {0} for member {1} with value {2}", JoinInProgressPeerId, Member.Name, CurrentValue);
            if (Reliable)
            {
                Instance.RsetId(JoinInProgressPeerId, Member.Name, CurrentValue);
            }
            else
            {
                Instance.RsetUnreliableId(JoinInProgressPeerId, Member.Name, CurrentValue);
            }
        }
    }

    public bool ShouldReplicate()
    {
        MasterAttribute MasterAtr = Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
        MasterSyncAttribute MasterSyncAtr = Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
        Node Node = NodeRef.GetRef() as Node;
        bool IsMaster = MDStatics.GetPeerId() == Node.GetNetworkMaster();
        if (!(IsMaster && MasterAtr == null && MasterSyncAtr == null) && !(IsMaster == false && (MasterAtr != null || MasterSyncAtr != null)))
        {
            return false;
        }

        return true;
    }

    public MDReplicatedType GetReplicatedType()
    {
        return ReplicatedType;
    }

    public bool IsReliable()
    {
        return Reliable;
    }
}
