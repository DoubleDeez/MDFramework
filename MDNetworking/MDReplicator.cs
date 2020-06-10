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
        List<ReplicatedMember> NodeMembers = new List<ReplicatedMember>();
        foreach(MemberInfo Member in Members)
        {
            MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                ReplicatedMember NodeMember = new ReplicatedMember();
                NodeMember.Member = Member;
                NodeMember.IsReliable = RepAttribute.Reliability == MDReliability.Reliable;
                NodeMember.ReplicatedType = RepAttribute.ReplicatedType;
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
                        ReplicatedMember RepMember = RepNode.Members[j];
                        MasterAttribute MasterAtr = RepMember.Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
                        MasterSyncAttribute MasterSyncAtr = RepMember.Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
                        bool IsMaster = MDStatics.GetPeerId() == Instance.GetNetworkMaster();
                        if (!(IsMaster && MasterAtr == null && MasterSyncAtr == null) && !(IsMaster == false && (MasterAtr != null || MasterSyncAtr != null)))
                        {
                            continue;
                        }

                        if (RepMember.ReplicatedType == MDReplicatedType.JoinInProgress && JIPPeerId == -1)
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

                        if (RepMember.ReplicatedType == MDReplicatedType.Always || object.Equals(RepMember.LastValue, CurrentValue) == false)
                        {
                            MDLog.Debug(LOG_CAT, "Replicating {0} with value {1} from {2}", RepMember.Member.Name, CurrentValue, RepMember.LastValue);
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
                        else if (JIPPeerId != -1)
                        {
                            MDLog.Debug(LOG_CAT, "Replicating to JIP Peer {0} for member {1} with value {2}", JIPPeerId, RepMember.Member.Name, CurrentValue);
                            if (RepMember.IsReliable)
                            {
                                Instance.RsetId(JIPPeerId, RepMember.Member.Name, CurrentValue);
                            }
                            else
                            {
                                Instance.RsetUnreliableId(JIPPeerId, RepMember.Member.Name, CurrentValue);
                            }
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

    public MDReplicatedType ReplicatedType;
}
