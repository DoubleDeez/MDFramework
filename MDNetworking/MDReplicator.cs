using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

[MDAutoRegister]
public class MDReplicator : Node
{
    private List<ReplicatedNode> NodeList = new List<ReplicatedNode>();
    private Queue<NewPlayer> JIPPlayers = new Queue<NewPlayer>();

    private NetworkKeyIdMap NetworkIdKeyMap = new NetworkKeyIdMap();

    private Dictionary<string, MDReplicatedMember> KeyToMemberMap = new Dictionary<string, MDReplicatedMember>();

    private const string LOG_CAT = "LogReplicator";
    public const float JIPWaitTime = 1000f;

    private uint LastIntervalReplication = 0;

    private uint ReplicationIdCounter = 0;

    public MDReplicator()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        MDOnScreenDebug.AddOnScreenDebugInfo("KeyToMemberMap Size", () => KeyToMemberMap.Count.ToString());
        MDOnScreenDebug.AddOnScreenDebugInfo("NetworkIDToKeyMap Size", () => NetworkIdKeyMap.GetCount().ToString());
    }

    public void OnPlayerJoined(int PeerId)
    {
        JIPPlayers.Enqueue(new NewPlayer(PeerId, OS.GetTicksMsec()));
        if (MDStatics.IsServer())
        {
            List<object> networkIdUpdates = new List<object>();
            foreach (uint key in NetworkIdKeyMap.GetKeys())
            {
                networkIdUpdates.Add(key);
                networkIdUpdates.Add(NetworkIdKeyMap.GetValue(key));
            }
            RpcId(PeerId, nameof(UpdateNetworkIdMap), networkIdUpdates);
        }
    }

    // Registers the given instance's fields marked with [MDReplicated()]
    public void RegisterReplication(Node Instance)
    {
        List<MemberInfo> Members = MDStatics.GetTypeMemberInfos(Instance);
        List<MDReplicatedMember> NodeMembers = new List<MDReplicatedMember>();
        foreach(MemberInfo Member in Members)
        {
            MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
            if (RepAttribute != null)
            {
                MDReplicatedMember NodeMember = CreateReplicatedMember(Member, RepAttribute, Instance);
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
            List<object> networkIdUpdates = new List<object>();
            foreach (MDReplicatedMember member in NodeMembers)
            {
                KeyToMemberMap.Add(member.GetUniqueKey(), member);
                if (MDStatics.IsServer())
                {
                    if (!NetworkIdKeyMap.ContainsKey(member.GetUniqueKey()))
                    {
                        uint networkid = GetReplicationId();
                        NetworkIdKeyMap.AddNetworkKeyIdPair(networkid, member.GetUniqueKey());
                        NetworkIdKeyMap.CheckBuffer(networkid, member);
                        networkIdUpdates.Add(networkid);
                        networkIdUpdates.Add(member.GetUniqueKey());
                    }
                }
            }
            if (networkIdUpdates.Count > 0)
            {
                Rpc(nameof(UpdateNetworkIdMap), networkIdUpdates);
            }
        }
    }

    // Unregisters the given instance's fields marked with [MDReplicated()]
    public void UnregisterReplication(Node Instance)
    {
        if (Godot.Object.IsInstanceValid(Instance))
        {
            NodeList.ForEach((repNode) => {
                if (repNode.Instance.GetRef() == Instance)
                {
                    NetworkIdKeyMap.RemoveMembers(repNode.Members);
                    foreach (MDReplicatedMember member in repNode.Members)
                    {
                        KeyToMemberMap.Remove(member.GetUniqueKey());
                    }
                }
            });
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
        bool isIntervalReplicationTime = CheckIntervalReplicationTime();

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
                        MDReplicatedMember RepMember = RepNode.Members[j];
                        RepMember.CheckForValueUpdate();
                        if (!RepMember.ShouldReplicate())
                        {
                            continue;
                        }

                        if (RepMember.GetReplicatedType() == MDReplicatedType.JoinInProgress && JIPPeerId == -1)
                        {
                            continue;
                        }
                        
                        RepMember.Replicate(JIPPeerId, isIntervalReplicationTime);
                    }
                }
                else
                {
                    NodeList.RemoveAt(i);
                }
            }
        }
    }

    ///<summary>Simple counter for interval replication</summary>
    private bool CheckIntervalReplicationTime()
    {
        if (LastIntervalReplication + GetReplicationIntervalMilliseconds() <= OS.GetTicksMsec())
        {
            LastIntervalReplication = OS.GetTicksMsec();
            return true;
        }

        return false;
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

    #region CLOCKED VALUE REPLICATION

    private uint GetReplicationId()
    {
        ReplicationIdCounter++;
        return ReplicationIdCounter;
    }

    public uint GetReplicationIdForKey(string Key)
    {
        return NetworkIdKeyMap.GetValue(Key);
    }

    ///<summary>Updates the clients network map</summary>
    [Remote]
    private void UpdateNetworkIdMap(params object[] updates)
    {
        for (int i=0; i < updates.Length; i = i+2)
        {
            String key = (string)updates[i+1];
            uint id = (uint)Int64.Parse(updates[i].ToString());
            NetworkIdKeyMap.AddNetworkKeyIdPair(id, key);
            if (KeyToMemberMap.ContainsKey(key))
            {
                NetworkIdKeyMap.CheckBuffer(id, KeyToMemberMap[key]);
            }
        }
    }


    [Remote]
    public void ReplicateClockedValue(uint ID, uint Tick, object Value)
    {
        String key = NetworkIdKeyMap.GetValue(ID);
        if (key == null || !KeyToMemberMap.ContainsKey(key))
        {
            // We got no key so add it to our buffer
            NetworkIdKeyMap.AddToBuffer(ID, Tick, Value);
            return;
        }

        KeyToMemberMap[key].SetValue(Tick, Value);
    }

    #endregion

    #region VIRTUAL METHODS

    ///<summary>Can be overwritten to provide custom replication types</summary>
    protected virtual MDReplicatedMember CreateReplicatedMember(MemberInfo Member, MDReplicated RepAttribute, Node Instance)
    {
        // Check if game clock is active, if so use it
        if (MDStatics.GetGameSynchronizer() != null && MDStatics.GetGameSynchronizer().IsGameClockActive())
        {
            PropertyInfo info = Instance.GetType().GetProperty(Member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            if (info.PropertyType == typeof(Vector2))
            {
                return new MDCRMInterpolatedVector2(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance));
            }
            return new MDClockedReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance));
        }
        
        return new MDReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance));
    }


    ///<summary>Returns the replication interval in milliseconds (Default: 100)</summary>
    protected virtual int GetReplicationIntervalMilliseconds()
    {
        return 300;
    }

    #endregion
}

class ReplicatedNode
{
    public ReplicatedNode(Node InInstance, List<MDReplicatedMember> InMembers)
    {
        Instance = Godot.Object.WeakRef(InInstance);
        Members = InMembers;
    }

    public WeakRef Instance;

    public List<MDReplicatedMember> Members;
}

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

class NetworkKeyIdMap
{
    private Dictionary<uint, string> NetworkIDToKeyMap = new Dictionary<uint, string>();
    private Dictionary<string, uint> KeyToNetworkIdMap = new Dictionary<string, uint>();

    // First dictionary uint is the network key, the inner uint is the tick
    private Dictionary<uint, Dictionary<uint, object>> ClockedValueBuffer = new Dictionary<uint, Dictionary<uint, object>>();

    public void AddNetworkKeyIdPair(uint id, string key)
    {
        NetworkIDToKeyMap.Add(id, key);
        KeyToNetworkIdMap.Add(key, id);
    }

    public IEnumerable<uint> GetKeys()
    {
        return NetworkIDToKeyMap.Keys;
    }

    public Dictionary<uint, object> GetBufferForId(uint ID)
    {
        if (!ClockedValueBuffer.ContainsKey(ID))
        {
            ClockedValueBuffer.Add(ID, new Dictionary<uint, object>());
        }

        return ClockedValueBuffer[ID];
    }

    public void AddToBuffer(uint ID, uint Tick, object Value)
    {
        Dictionary<uint, object> buffer = GetBufferForId(ID);
        buffer.Add(Tick, Value);
    }

    public void CheckBuffer(uint ID, MDReplicatedMember Member)
    {
        if (ClockedValueBuffer.ContainsKey(ID))
        {
            Dictionary<uint, object> buffer = GetBufferForId(ID);
            foreach (uint tick in buffer.Keys)
            {
                Member.SetValue(tick, buffer[tick]);
            }
            buffer.Clear();
            ClockedValueBuffer.Remove(ID);
        }
    }

    protected void RemoveBufferId(uint ID)
    {
        if (ClockedValueBuffer.ContainsKey(ID))
        {
            ClockedValueBuffer.Remove(ID);
        }
    }

    public string GetValue(uint id)
    {
        if (NetworkIDToKeyMap.ContainsKey(id))
        {
            return NetworkIDToKeyMap[id];
        }

        return null;
    }

    public uint GetValue(string key)
    {
        if (KeyToNetworkIdMap.ContainsKey(key))
        {
            return KeyToNetworkIdMap[key];
        }
        
        return 0;
    }

    public int GetCount()
    {
        return KeyToNetworkIdMap.Count;
    }

    public bool ContainsKey(String key)
    {
        return KeyToNetworkIdMap.ContainsKey(key);
    }

    public bool ContainsKey(uint id)
    {
        return NetworkIDToKeyMap.ContainsKey(id);
    }

    public void RemoveValue(string key)
    {
        if (KeyToNetworkIdMap.ContainsKey(key))
        {
            NetworkIDToKeyMap.Remove(KeyToNetworkIdMap[key]);
            RemoveBufferId(KeyToNetworkIdMap[key]);
            KeyToNetworkIdMap.Remove(key);
        }
    }

    public void RemoveValue(uint id)
    {
        if (NetworkIDToKeyMap.ContainsKey(id))
        {
            KeyToNetworkIdMap.Remove(NetworkIDToKeyMap[id]);
            RemoveBufferId(id);
            NetworkIDToKeyMap.Remove(id);
        }
    }

    public void RemoveMembers(List<MDReplicatedMember> members)
    {
        foreach (MDReplicatedMember member in members)
        {
            RemoveValue(member.GetUniqueKey());
        }
    }
}