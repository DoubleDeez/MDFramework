using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

[MDAutoRegister]
public class MDReplicator : Node
{
    public enum Settings
    {
        ProcessWhilePaused,
        GroupName,
        ReplicatedMemberType
    }
    private List<ReplicatedNode> NodeList = new List<ReplicatedNode>();
    private Queue<NewPlayer> JIPPlayers = new Queue<NewPlayer>();

    private MDReplicatorNetworkKeyIdMap NetworkIdKeyMap = new MDReplicatorNetworkKeyIdMap();

    private MDReplicatorGroupManager GroupManager = new MDReplicatorGroupManager();

    private Dictionary<string, MDReplicatedMember> KeyToMemberMap = new Dictionary<string, MDReplicatedMember>();

    private const string LOG_CAT = "LogReplicator";
    public const float JIPWaitTime = 1000f;

    private uint LastIntervalReplication = 0;

    private uint ReplicationIdCounter = 0;

    public override void _Ready()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        MDOnScreenDebug.AddOnScreenDebugInfo("KeyToMemberMap Size", () => KeyToMemberMap.Count.ToString());
        MDOnScreenDebug.AddOnScreenDebugInfo("NetworkIDToKeyMap Size", () => NetworkIdKeyMap.GetCount().ToString());
        this.GetGameSession().OnSessionStartedEvent += OnSessionStarted;
        this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoined;
        PauseMode = PauseModeEnum.Process;

        GetTree().Connect("idle_frame", this, nameof(TickReplication));
    }

    public override void _ExitTree()
    {
        this.GetGameSession().OnSessionStartedEvent -= OnSessionStarted;
        this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoined;
    }

    private void OnSessionStarted()
    {
        // Reset the NetworkKeyIdMap on new session started
        NetworkIdKeyMap = new MDReplicatorNetworkKeyIdMap();
    }

    public void OnPlayerJoined(int PeerId)
    {
        // Skip local player
        if (PeerId == MDStatics.GetPeerId())
        {
            return;
        }

        MDLog.Debug(LOG_CAT, "Registered JIPPlayer with Id", PeerId);
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

    // Returns an array of settings for the member
    private MDReplicatedSetting[] GetSettings(MemberInfo Member)
    {
        object[] Settings = Member.GetCustomAttributes(typeof(MDReplicatedSetting), true);
        return Array.ConvertAll(Settings, item => (MDReplicatedSetting)item);
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
                MDReplicatedSetting[] Settings = GetSettings(Member);
                MDReplicatedMember NodeMember = CreateReplicatedMember(Member, RepAttribute, Instance, Settings);
                
                NodeMembers.Add(NodeMember);

                ProcessSettingsForMember(NodeMember, ParseParameters(typeof(Settings), Settings));

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

    /// Process our settings
    private void ProcessSettingsForMember(MDReplicatedMember ReplicatedMember, MDReplicatedSetting[] Settings)
    {
        foreach (MDReplicatedSetting setting in Settings)
        {
            switch ((Settings)setting.Key)
            {
                case MDReplicator.Settings.ProcessWhilePaused:
                    ReplicatedMember.ProcessWhilePaused = (bool)setting.Value;
                    break;
                case MDReplicator.Settings.GroupName:
                    ReplicatedMember.ReplicationGroup = setting.Value.ToString();
                    break;
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
        bool paused = GetTree().Paused;

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

                        if (paused && !RepMember.ProcessWhilePaused)
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

    [Remote]
    public void ReplicateClockedValues(uint ID, uint Tick, params object[] Parameters)
    {
        String key = NetworkIdKeyMap.GetValue(ID);
        if (key == null || !KeyToMemberMap.ContainsKey(key))
        {
            // We got no key so add it to our buffer
            NetworkIdKeyMap.AddToBuffer(ID, Tick, Parameters);
            return;
        }

        KeyToMemberMap[key].SetValues(Tick, Parameters);
    }

    #endregion

    #region VIRTUAL METHODS

    ///<summary>Can be overwritten to provide custom replication types</summary>
    protected virtual MDReplicatedMember CreateReplicatedMember(MemberInfo Member, MDReplicated RepAttribute, Node Instance, MDReplicatedSetting[] Settings)
    {
        Type ReplicatedMemberTypeOverride = GetReplicatedMemberOverrideType(ParseParameters(typeof(Settings), Settings));
        if (ReplicatedMemberTypeOverride != null && ReplicatedMemberTypeOverride.IsAssignableFrom(typeof(MDReplicatedMember)))
        {
            return Activator.CreateInstance(ReplicatedMemberTypeOverride, 
                    new object[] {Member, RepAttribute.Reliability == MDReliability.Reliable, 
                                    RepAttribute.ReplicatedType, WeakRef(Instance), Settings}) as MDReplicatedMember;
        }

        // Check if game clock is active, if so use it
        if (MDStatics.GetGameSynchronizer() != null && MDStatics.GetGameSynchronizer().IsGameClockActive())
        {
            if (Member.GetUnderlyingType() == typeof(Vector2))
            {
                return new MDCRMInterpolatedVector2(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
            }
            return new MDClockedReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
        }
        
        return new MDReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
    }

    private Type GetReplicatedMemberOverrideType(MDReplicatedSetting[] Settings)
    {
        foreach (MDReplicatedSetting setting in Settings)
        {
            if ((Settings)setting.Key == MDReplicator.Settings.ReplicatedMemberType)
            {
                if (setting.Value == null)
                {
                    return null;
                }
                return Type.GetType(setting.Value.ToString());
            }
        }
        return null;
    }


    ///<summary>Returns the replication interval in milliseconds (Default: 100)</summary>
    protected virtual int GetReplicationIntervalMilliseconds()
    {
        return 100;
    }

    #endregion

    #region SUPPORT METHODS

    ///<Summary>Look for settings with keys of the specified type</summary>
    public static MDReplicatedSetting[] ParseParameters(Type TypeToLookFor, MDReplicatedSetting[] Parameters)
    {
        List<MDReplicatedSetting> SettingsList = new List<MDReplicatedSetting>();
        foreach (MDReplicatedSetting setting in Parameters)
        {
            if (setting.Key.GetType() == TypeToLookFor)
            {
                SettingsList.Add(setting);
            }
        }
        return SettingsList.ToArray();
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
