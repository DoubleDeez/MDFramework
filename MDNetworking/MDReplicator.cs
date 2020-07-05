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

    private MDReplicatorGroupManager GroupManager;

    private Dictionary<string, MDReplicatedMember> KeyToMemberMap = new Dictionary<string, MDReplicatedMember>();

    private List<ClockedRemoteCall> ClockedRemoteCallList = new List<ClockedRemoteCall>();

    private const string LOG_CAT = "LogReplicator";
    public const float JIPWaitTime = 1000f;

    private uint ReplicationIdCounter = 0;

    private MDGameClock GameClock;

    public override void _Ready()
    {
        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
        MDOnScreenDebug.AddOnScreenDebugInfo("KeyToMemberMap Size", () => KeyToMemberMap.Count.ToString());
        MDOnScreenDebug.AddOnScreenDebugInfo("NetworkIDToKeyMap Size", () => NetworkIdKeyMap.GetCount().ToString());
        this.GetGameSession().OnSessionStartedEvent += OnSessionStarted;
        this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoined;
        PauseMode = PauseModeEnum.Process;

        GroupManager = new MDReplicatorGroupManager(GetReplicationFrameInterval());

        GameClock = this.GetGameClock();
    }

    public override void _ExitTree()
    {
        this.GetGameSession().OnSessionStartedEvent -= OnSessionStarted;
        this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoined;
    }

    public override void _PhysicsProcess(float delta)
    {
        TickReplication();
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

                GroupManager.AddReplicatedMember(NodeMember);

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
                        GroupManager.RemoveReplicatedMember(member);
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
        bool paused = GetTree().Paused;

        #if DEBUG
        using (MDProfiler Profiler = new MDProfiler("MDReplicator.TickReplication"))
        #endif
        {
            if (MDStatics.IsNetworkActive() == false)
            {
                return;
            }

            // First process any outstanding clocked calls
            CheckClockedRemoteCalls();

            int JIPPeerId = CheckForNewPlayer();

            HashSet<MDReplicatedMember> CurrentReplicationList = GroupManager.GetMembersToReplicate();

            for (int i = NodeList.Count - 1; i >= 0; --i)
            {
                ReplicatedNode RepNode = NodeList[i];
                Node Instance = RepNode.Instance.GetRef() as Node;
                if (Godot.Object.IsInstanceValid(Instance))
                {
                    RepNode.CheckIfNetworkMasterChanged(Instance.GetNetworkMaster());

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
                        
                        RepMember.Replicate(JIPPeerId, CurrentReplicationList.Contains(RepMember));
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

        // Check if we got a Command Replicator
        if (Member.GetUnderlyingType().IsAssignableFrom(typeof(IMDCommandReplicator)))
        {
            return new MDReplicatedCommandReplicator(Member, RepAttribute.Reliability == MDReliability.Reliable, RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
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


    /// <summary>Interval replication happens every X physic frames. One physics frame is by default about 16 msec (Default: X=6).</summary>
    protected virtual int GetReplicationFrameInterval()
    {
        return 6;
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

    #region RPC CALLS WITH GAME CLOCK

    public void SendClockedRpc(int PeerId, MDReliability Reliability, Node Target, String Method, params object[] Parameters)
    {
        MDRemoteMode Mode = MDStatics.GetMethodRpcType(Target, Method);
        switch (Mode)
        {
            case MDRemoteMode.Master:
                if (!Target.IsNetworkMaster())
                {
                    // Remote invoke master only
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method, Mode, Parameters);
                }
                break;
            case MDRemoteMode.MasterSync:
                if (!Target.IsNetworkMaster())
                {
                    // Remote invoke master only
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method, Mode, Parameters);
                }
                Target.Invoke(Method, Parameters);
                break;
            case MDRemoteMode.Puppet:
            case MDRemoteMode.Remote:
                // Remote invoke
                SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method, Mode, Parameters);
                break;
            case MDRemoteMode.PuppetSync:
            case MDRemoteMode.RemoteSync:
                // Remote invoke and local invoke
                SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method, Mode, Parameters);
                Target.Invoke(Method, Parameters);
                break;
        }
    }

    public void SendClockedRset(int PeerId, MDReliability Reliability, Node Target, String MemberName, object Value)
    {
        MDRemoteMode Mode = MDStatics.GetMemberRpcType(Target, MemberName);
        switch (Mode)
        {
            case MDRemoteMode.Master:
                if (!Target.IsNetworkMaster())
                {
                    // Remote invoke master only
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(), MemberName, Mode, Value);
                }
                break;
            case MDRemoteMode.MasterSync:
                if (!Target.IsNetworkMaster())
                {
                    // Remote invoke master only
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(), MemberName, Mode, Value);
                }
                Target.SetMemberValue(MemberName, Value);
                break;
            case MDRemoteMode.Puppet:
            case MDRemoteMode.Remote:
                // Remote invoke
                SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(), MemberName, Mode, Value);
                break;
            case MDRemoteMode.PuppetSync:
            case MDRemoteMode.RemoteSync:
                // Remote invoke and local invoke
                SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(), MemberName, Mode, Value);
                Target.SetMemberValue(MemberName, Value);
                break;
        }
    }

    private void SendClockedCall(int PeerId, ClockedRemoteCall.TypeOfCall Type, MDReliability Reliability, 
                                 String NodePath, String Method, MDRemoteMode Mode, params object[] Parameters)
    {
        if (Reliability == MDReliability.Reliable)
        {
            if (PeerId != -1)
            {
                RpcId(PeerId, nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode, Parameters);
            }
            else
            {
                Rpc(nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode, Parameters);
            }
        }
        else
        {
            if (PeerId != -1)
            {
                RpcUnreliableId(PeerId, nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode, Parameters);
            }
            else
            {
                RpcUnreliable(nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode, Parameters);
            }
        }
    }

    [Remote]
    private void ClockedCall(uint Tick, ClockedRemoteCall.TypeOfCall Type, String NodePath, String Method, MDRemoteMode Mode, params object[] Parameters)
    {
        Node Target = GetNodeOrNull(NodePath);
        if (Target == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find target [{0}] for ClockedRpcCall.", NodePath);
            return;
        }
        ClockedRemoteCall RemoteCall = new ClockedRemoteCall(Tick, Type, WeakRef(Target), Method, Mode, Parameters);

        // Check if we should already invoke this (if the time has already passed)
        if (!RemoteCall.Invoke(GameClock.GetRemoteTick()))
        {
            ClockedRemoteCallList.Add(RemoteCall);
        }
    }

    private void CheckClockedRemoteCalls()
    {
        uint Tick = GameClock.GetRemoteTick();
        // Loop in reverse so we can remove during loop
        for (int i = ClockedRemoteCallList.Count - 1; i >= 0; i--)
        {
            if (ClockedRemoteCallList[i].Invoke(Tick))
            {
                ClockedRemoteCallList.RemoveAt(i);
            }
        }
    }


    #endregion
}

class ClockedRemoteCall
{
    public enum TypeOfCall
    {
        RSET,
        RPC
    }
    private const String LOG_CAT = "LogClockedRemoteCall";
    private uint Tick;
    private WeakRef Node;
    private String Name;
    private MDRemoteMode Mode;
    private object[] Parameters;
    private TypeOfCall Type;

    public ClockedRemoteCall(uint Tick, TypeOfCall Type, WeakRef Node, String Name, MDRemoteMode Mode, params object[] Parameters)
    {
        this.Tick = Tick;
        this.Type = Type;
        this.Node = Node;
        this.Name = Name;
        this.Mode = Mode;
        this.Parameters = Parameters;
    }

    /// Returns true once we have invoked or if we can't invoke
    public bool Invoke(uint Tick)
    {
        if (this.Tick <= Tick)
        {
            if (Node.GetRef() == null)
            {
                MDLog.Warn(LOG_CAT, "Node no longer exists for call");
                return true;
            }

            Node Target = Node.GetRef() as Node;
            switch (Mode)
            {
                case MDRemoteMode.Master:
                case MDRemoteMode.MasterSync:
                    if (Target.IsNetworkMaster())
                    {
                        DoCall(Target);
                    }
                    break;
                case MDRemoteMode.PuppetSync:
                case MDRemoteMode.Puppet:
                    if (!Target.IsNetworkMaster())
                    {
                        DoCall(Target);
                    }
                    break;
                case MDRemoteMode.Remote:
                case MDRemoteMode.RemoteSync:
                    DoCall(Target);
                    break;
            }
            
            return true;
        }
        return false;
    }

    private void DoCall(Node Target)
    {
        switch (Type)
        {
            case TypeOfCall.RPC:
                Target.Invoke(Name, Parameters);
                break;
            case TypeOfCall.RSET:
                Target.SetMemberValue(Name, Parameters[0]);
                break;
        }
    }

}

class ReplicatedNode
{
    public ReplicatedNode(Node InInstance, List<MDReplicatedMember> InMembers)
    {
        Instance = Godot.Object.WeakRef(InInstance);
        Members = InMembers;
        NetworkMaster = InInstance.GetNetworkMaster();
    }

    public void CheckIfNetworkMasterChanged(int CurrentMaster)
    {
        if (CurrentMaster != NetworkMaster)
        {
            Members.ForEach(member => member.CheckIfShouldReplicate());
            NetworkMaster = CurrentMaster;
        }
    }

    protected int NetworkMaster;

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
