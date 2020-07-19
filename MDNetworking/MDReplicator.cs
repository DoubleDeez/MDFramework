using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace MD
{

    internal struct NewPlayer
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
            else if (JoinTime + MDReplicator.JIPWaitTime < OS.GetTicksMsec())
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

    internal class ClockedRemoteCall
    {
        public enum TypeOfCall
        {
            RSET,
            RPC
        }

        private const string LOG_CAT = "LogClockedRemoteCall";
        private uint Tick;
        private WeakRef Node;
        private string Name;
        private MDRemoteMode Mode;
        private object[] Parameters;
        private TypeOfCall Type;

        public ClockedRemoteCall(uint Tick, TypeOfCall Type, WeakRef Node, String Name, MDRemoteMode Mode,
            params object[] Parameters)
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
            if (this.Tick > Tick)
            {
                return false;
            }

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

    internal class ReplicatedNode
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

    /// <summary>
    /// The class that is in charge of replicating values across the network.
    /// </summary>
    [MDAutoRegister]
    public class MDReplicator : Node
    {
        public enum Settings
        {
            ProcessWhilePaused,
            GroupName,
            ReplicatedMemberType,
            Interpolate
        }

        public const String REPLICATE_METHOD_NAME = nameof(ReplicateClockedValues);

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

        public void Initialize()
        {
            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));
            MDOnScreenDebug.AddOnScreenDebugInfo("KeyToMemberMap Size", () => KeyToMemberMap.Count.ToString());
            MDOnScreenDebug.AddOnScreenDebugInfo("NetworkIDToKeyMap Size", () => NetworkIdKeyMap.GetCount().ToString());
            this.GetGameSession().OnSessionEndedEvent += OnSessionEnded;
            this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoined;
            PauseMode = PauseModeEnum.Process;

            GroupManager = new MDReplicatorGroupManager(GetReplicationFrameInterval());

            GameClock = this.GetGameClock();
        }

        public override void _ExitTree()
        {
            this.GetGameSession().OnSessionEndedEvent -= OnSessionEnded;
            this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoined;
        }

        public override void _PhysicsProcess(float delta)
        {
            TickReplication();
        }

        private void OnSessionEnded()
        {
            NetworkIdKeyMap = new MDReplicatorNetworkKeyIdMap();
            KeyToMemberMap = new Dictionary<string, MDReplicatedMember>();
        }

        private void OnPlayerJoined(int PeerId)
        {
            // Skip local player
            if (PeerId == MDStatics.GetPeerId())
            {
                return;
            }

            MDLog.Debug(LOG_CAT, $"Registered JIPPlayer with Id: {PeerId}");
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
            return Array.ConvertAll(Settings, item => (MDReplicatedSetting) item);
        }

        /// <summary>
        /// Registers the given instance's fields marked with [MDReplicated()]
        /// </summary>
        /// <param name="Instance">The node to register</param>
        public void RegisterReplication(Node Instance)
        {
            List<MemberInfo> Members = MDStatics.GetTypeMemberInfos(Instance);
            List<MDReplicatedMember> NodeMembers = new List<MDReplicatedMember>();
            foreach (MemberInfo Member in Members)
            {
                MDReplicated RepAttribute = Member.GetCustomAttribute(typeof(MDReplicated)) as MDReplicated;
                if (RepAttribute == null)
                {
                    continue;
                }

                MDReplicatedSetting[] Settings = GetSettings(Member);
                MDReplicatedMember NodeMember = CreateReplicatedMember(Member, RepAttribute, Instance, Settings);

                NodeMembers.Add(NodeMember);

                ProcessSettingsForMember(NodeMember, ParseParameters(typeof(Settings), Settings));

                GroupManager.AddReplicatedMember(NodeMember);

                MDLog.Debug(LOG_CAT, $"Adding Replicated Node {Instance.Name} Member {Member.Name}");

                if (HasRPCModeSet(Member) == false)
                {
                    Instance.RsetConfig(Member.Name, MultiplayerAPI.RPCMode.Puppet);
                }
            }

            if (NodeMembers.Count > 0)
            {
                NodeList.Add(new ReplicatedNode(Instance, NodeMembers));
                List<object> networkIdUpdates = new List<object>();
                foreach (MDReplicatedMember member in NodeMembers)
                {
                    string MemberUniqueKey = member.GetUniqueKey();
                    KeyToMemberMap.Add(MemberUniqueKey, member);

                    // Check if we have a buffer waiting for this member
                    if (NetworkIdKeyMap.ContainsKey(MemberUniqueKey))
                    {
                        NetworkIdKeyMap.CheckBuffer(NetworkIdKeyMap.GetValue(MemberUniqueKey), member);
                    }

                    if (MDStatics.IsServer())
                    {
                        if (!NetworkIdKeyMap.ContainsKey(member.GetUniqueKey()))
                        {
                            uint networkid = GetReplicationId();
                            MDLog.Trace(LOG_CAT, $"Adding NetworkIdKeyMap key [{member.GetUniqueKey()}] with id [{networkid}]");
                            NetworkIdKeyMap.AddNetworkKeyIdPair(networkid, member.GetUniqueKey());
                            NetworkIdKeyMap.CheckBuffer(networkid, member);
                            networkIdUpdates.Add(networkid);
                            networkIdUpdates.Add(member.GetUniqueKey());
                        }
                    }
                }

                if (MDStatics.IsNetworkActive() && networkIdUpdates.Count > 0)
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
                switch ((Settings) setting.Key)
                {
                    case MDReplicator.Settings.ProcessWhilePaused:
                        ReplicatedMember.ProcessWhilePaused = (bool) setting.Value;
                        break;
                    case MDReplicator.Settings.GroupName:
                        ReplicatedMember.ReplicationGroup = setting.Value.ToString();
                        break;
                }
            }
        }

        /// <summary>
        /// Unregisters the given instance's fields marked with [MDReplicated()]
        /// </summary>
        /// <param name="Instance">The node to unregister</param>
        public void UnregisterReplication(Node Instance)
        {
            if (!IsInstanceValid(Instance))
            {
                return;
            }

            foreach (ReplicatedNode repNode in NodeList)
            {
                if (repNode.Instance.GetRef() != Instance)
                {
                    return;
                }

                NetworkIdKeyMap.RemoveMembers(repNode.Members);
                foreach (MDReplicatedMember member in repNode.Members)
                {
                    GroupManager.RemoveReplicatedMember(member);
                    KeyToMemberMap.Remove(member.GetUniqueKey());
                }
            }

            NodeList.RemoveAll(RepNode => RepNode.Instance.GetRef() == Instance);
        }

        // Peeks the JIPPlayer queue and returns the first peerid if enough time has passed
        private int CheckForNewPlayer()
        {
            if (JIPPlayers.Count > 0)
            {
                NewPlayer JIPPlayer = JIPPlayers.Peek();
                if (JIPPlayer.IsReadyForReplication())
                {
                    MDLog.Debug(LOG_CAT, $"JIP Peer Id {JIPPlayer.PeerId} ready for MDReplicated");
                    return JIPPlayers.Dequeue().PeerId;
                }
            }

            return -1;
        }

        // Broadcasts out replicated modified variables if we're the server, propagates changes recieved from the server if client.
        private void TickReplication()
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
                    if (!IsInstanceValid(Instance))
                    {
                        NodeList.RemoveAt(i);
                    }
                    else
                    {
                        RepNode.CheckIfNetworkMasterChanged(Instance.GetNetworkMaster());

                        foreach (MDReplicatedMember RepMember in RepNode.Members)
                        {
                            RepMember.CheckForValueUpdate();
                            if (!RepMember.ShouldReplicate()
                                || paused && !RepMember.ProcessWhilePaused
                                || RepMember.GetReplicatedType() == MDReplicatedType.JoinInProgress && JIPPeerId == -1)
                            {
                                continue;
                            }

                            MDLog.CTrace(JIPPeerId != -1, LOG_CAT, $"Replicating {RepMember.GetUniqueKey()} to JIP Player {JIPPeerId}");
                            RepMember.Replicate(JIPPeerId, CurrentReplicationList.Contains(RepMember));
                        }
                    }
                }
            }
        }

        private bool HasRPCModeSet(MemberInfo Member)
        {
            MasterAttribute MasterAtr = Member.GetCustomAttribute(typeof(MasterAttribute)) as MasterAttribute;
            MasterSyncAttribute MasterSyncAtr =
                Member.GetCustomAttribute(typeof(MasterSyncAttribute)) as MasterSyncAttribute;
            PuppetAttribute PuppetAtr = Member.GetCustomAttribute(typeof(PuppetAttribute)) as PuppetAttribute;
            PuppetSyncAttribute PuppetSyncAtr =
                Member.GetCustomAttribute(typeof(PuppetSyncAttribute)) as PuppetSyncAttribute;
            RemoteAttribute RemoteAtr = Member.GetCustomAttribute(typeof(RemoteAttribute)) as RemoteAttribute;
            RemoteSyncAttribute RemoteSyncAtr =
                Member.GetCustomAttribute(typeof(RemoteSyncAttribute)) as RemoteSyncAttribute;

            return MasterAtr != null || MasterSyncAtr != null || PuppetAtr != null || PuppetSyncAtr != null ||
                   RemoteAtr != null || RemoteSyncAtr != null;
        }

        #region CLOCKED VALUE REPLICATION

        private uint GetReplicationId()
        {
            ReplicationIdCounter++;
            return ReplicationIdCounter;
        }

        /// <summary>
        /// Get a unique ID for the key string
        /// </summary>
        /// <param name="Key">The key to get unique id for</param>
        /// <returns>The unique Uint ID</returns>
        public uint GetReplicationIdForKey(string Key)
        {
            return NetworkIdKeyMap.GetValue(Key);
        }

        // Updates the clients network map
        [Remote]
        private void UpdateNetworkIdMap(params object[] updates)
        {
            for (int i = 0; i < updates.Length; i += 2)
            {
                string key = (string) updates[i + 1];
                uint id = (uint) long.Parse(updates[i].ToString());
                MDLog.Debug(LOG_CAT, $"Received Network Map Update with id {id} and key [{key}]");
                NetworkIdKeyMap.AddNetworkKeyIdPair(id, key);
                if (KeyToMemberMap.ContainsKey(key))
                {
                    NetworkIdKeyMap.CheckBuffer(id, KeyToMemberMap[key]);
                }
                else
                {
                    MDLog.Trace(LOG_CAT, $"KeyToMemberMap does not contain key {key}");
                }
            }
        }

        /// <summary>
        /// Replicates a clocked value to another client
        /// </summary>
        /// <param name="ID">The ID of this clocked value</param>
        /// <param name="Tick">The tick this was replicated</param>
        /// <param name="Parameters">The parameters representing this clocked value</param>
        [Remote]
        public void ReplicateClockedValues(uint ID, uint Tick, params object[] Parameters)
        {
            string key = NetworkIdKeyMap.GetValue(ID);
            if (key == null || !KeyToMemberMap.ContainsKey(key))
            {
                MDLog.Debug(LOG_CAT, $"Received replication for id {ID} and tick {Tick} not in map");
                // We got no key so add it to our buffer
                NetworkIdKeyMap.AddToBuffer(ID, Tick, Parameters);
                return;
            }

            KeyToMemberMap[key].SetValues(Tick, Parameters);
        }

        #endregion

        #region VIRTUAL METHODS

        /// <summary>
        /// Can be overwritten to provide custom replication types
        /// </summary>
        /// <param name="Member">The member info to create a replicated member for</param>
        /// <param name="RepAttribute">The replication reliability we want</param>
        /// <param name="Instance">The node this member is on</param>
        /// <param name="Settings">Any settings we got for this member</param>
        /// <returns>A new replicated member</returns>
        protected virtual MDReplicatedMember CreateReplicatedMember(MemberInfo Member, MDReplicated RepAttribute,
            Node Instance, MDReplicatedSetting[] Settings)
        {
            MDReplicatedSetting[] ParsedSettings = ParseParameters(typeof(Settings), Settings);
            Type ReplicatedMemberTypeOverride =
                GetReplicatedMemberOverrideType(ParsedSettings);
            if (ReplicatedMemberTypeOverride != null &&
                ReplicatedMemberTypeOverride.IsAssignableFrom(typeof(MDReplicatedMember)))
            {
                return Activator.CreateInstance(ReplicatedMemberTypeOverride,
                    new object[]
                    {
                        Member, RepAttribute.Reliability == MDReliability.Reliable,
                        RepAttribute.ReplicatedType, WeakRef(Instance), Settings
                    }) as MDReplicatedMember;
            }

            // Check if we got a Command Replicator
            if (Member.GetUnderlyingType().GetInterface(nameof(IMDCommandReplicator)) != null)
            {
                return new MDReplicatedCommandReplicator(Member, RepAttribute.Reliability == MDReliability.Reliable, 
                            RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
            }

            // Check if game clock is active, if so use it
            bool AllowInterpolation = GetAllowsInterpolation(ParsedSettings);
            if (MDStatics.GetGameSynchronizer() != null && MDStatics.IsGameClockActive() && AllowInterpolation)
            {
                if (Member.GetUnderlyingType() == typeof(float))
                {
                    return new MDCRMInterpolatedFloat(Member, RepAttribute.Reliability == MDReliability.Reliable,
                        RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
                }

                if (Member.GetUnderlyingType() == typeof(Vector2))
                {
                    return new MDCRMInterpolatedVector2(Member, RepAttribute.Reliability == MDReliability.Reliable,
                        RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
                }
                
                if (Member.GetUnderlyingType() == typeof(Vector3))
                {
                    return new MDCRMInterpolatedVector3(Member, RepAttribute.Reliability == MDReliability.Reliable,
                        RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
                }
            }

            return new MDReplicatedMember(Member, RepAttribute.Reliability == MDReliability.Reliable,
                RepAttribute.ReplicatedType, WeakRef(Instance), Settings);
        }

        private Type GetReplicatedMemberOverrideType(MDReplicatedSetting[] Settings)
        {
            foreach (MDReplicatedSetting setting in Settings)
            {
                if ((Settings) setting.Key == MDReplicator.Settings.ReplicatedMemberType)
                {
                    return setting.Value != null ? Type.GetType(setting.Value.ToString()) : null;
                }
            }

            return null;
        }

        private bool GetAllowsInterpolation(MDReplicatedSetting[] Settings)
        {
            foreach (MDReplicatedSetting setting in Settings)
            {
                if ((Settings) setting.Key == MDReplicator.Settings.Interpolate)
                {
                    return setting.Value != null ? Convert.ToBoolean(setting.Value) : true;
                }
            }

            return true;
        }


        /// <summary>
        /// Interval replication happens every X physic frames. One physics frame is by default about 16 msec (Default: X=6).
        /// </summary>
        /// <returns>A frame interval</returns>
        protected virtual int GetReplicationFrameInterval()
        {
            return this.GetConfiguration().GetInt(MDConfiguration.ConfigurationSections.Replicator, MDConfiguration.FRAME_INTERVAL, 6);
        }

        #endregion

        #region SUPPORT METHODS

        /// <summary>
        /// Look for settings with keys of the specified type
        /// </summary>
        /// <param name="TypeToLookFor">The key type to look for</param>
        /// <param name="Parameters">The parameters to look through</param>
        /// <returns>A list only containing the parameters with a key of the type given</returns>
        public static MDReplicatedSetting[] ParseParameters(Type TypeToLookFor, MDReplicatedSetting[] Parameters)
        {
            return Parameters.Where(setting => setting.Key.GetType() == TypeToLookFor).ToArray();
        }

        #endregion

        #region RPC CALLS WITH GAME CLOCK

        /// <summary>
        /// Sends a clocked RPC call to another client
        /// </summary>
        /// <param name="PeerId">The peer to send to</param>
        /// <param name="Reliability">Reliability to send at</param>
        /// <param name="Target">The node that is the target of our rpc call</param>
        /// <param name="Method">The method we want to invoke on the node</param>
        /// <param name="Parameters">Parameters for the call</param>
        public void SendClockedRpc(int PeerId, MDReliability Reliability, Node Target, string Method,
            params object[] Parameters)
        {
            MDRemoteMode Mode = MDStatics.GetMethodRpcType(Target, Method, Parameters);
            switch (Mode)
            {
                case MDRemoteMode.Master:
                    if (!Target.IsNetworkMaster())
                    {
                        // Remote invoke master only
                        SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method,
                            Mode, Parameters);
                    }

                    break;
                case MDRemoteMode.MasterSync:
                    if (!Target.IsNetworkMaster())
                    {
                        // Remote invoke master only
                        SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method,
                            Mode, Parameters);
                    }

                    Target.Invoke(Method, Parameters);
                    break;
                case MDRemoteMode.Puppet:
                case MDRemoteMode.Remote:
                    // Remote invoke
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method,
                        Mode, Parameters);
                    break;
                case MDRemoteMode.PuppetSync:
                case MDRemoteMode.RemoteSync:
                    // Remote invoke and local invoke
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RPC, Reliability, Target.GetPath(), Method,
                        Mode, Parameters);
                    Target.Invoke(Method, Parameters);
                    break;
            }
        }

        /// <summary>
        /// Sends a clocked rset to another client
        /// </summary>
        /// <param name="PeerId">The peer to send to</param>
        /// <param name="Reliability">Reliability to send at</param>
        /// <param name="Target">The node that is the target of our rpc call</param>
        /// <param name="MemberName">The name of the member to set</param>
        /// <param name="Value">The value to set</param>
        public void SendClockedRset(int PeerId, MDReliability Reliability, Node Target, string MemberName, object Value)
        {
            MDRemoteMode Mode = MDStatics.GetMemberRpcType(Target, MemberName);
            switch (Mode)
            {
                case MDRemoteMode.Master:
                    if (!Target.IsNetworkMaster())
                    {
                        // Remote invoke master only
                        SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(),
                            MemberName, Mode, Value);
                    }

                    break;
                case MDRemoteMode.MasterSync:
                    if (!Target.IsNetworkMaster())
                    {
                        // Remote invoke master only
                        SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(),
                            MemberName, Mode, Value);
                    }

                    Target.SetMemberValue(MemberName, Value);
                    break;
                case MDRemoteMode.Puppet:
                case MDRemoteMode.Remote:
                    // Remote invoke
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(),
                        MemberName, Mode, Value);
                    break;
                case MDRemoteMode.PuppetSync:
                case MDRemoteMode.RemoteSync:
                    // Remote invoke and local invoke
                    SendClockedCall(PeerId, ClockedRemoteCall.TypeOfCall.RSET, Reliability, Target.GetPath(),
                        MemberName, Mode, Value);
                    Target.SetMemberValue(MemberName, Value);
                    break;
            }
        }

        private void SendClockedCall(int PeerId, ClockedRemoteCall.TypeOfCall Type, MDReliability Reliability,
            string NodePath, string Method, MDRemoteMode Mode, params object[] Parameters)
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
                    RpcUnreliableId(PeerId, nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode,
                        Parameters);
                }
                else
                {
                    RpcUnreliable(nameof(ClockedCall), GameClock.GetTick(), Type, NodePath, Method, Mode, Parameters);
                }
            }
        }

        [Remote]
        private void ClockedCall(uint Tick, ClockedRemoteCall.TypeOfCall Type, string NodePath, string Method,
            MDRemoteMode Mode, params object[] Parameters)
        {
            Node Target = GetNodeOrNull(NodePath);
            if (Target == null)
            {
                MDLog.Warn(LOG_CAT, $"Could not find target [{NodePath}] for ClockedRpcCall.");
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
}