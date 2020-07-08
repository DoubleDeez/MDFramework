using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MD
{
    ///<summary>The command replicator can replicate custom commands across the network</summary>
    public interface IMDCommandReplicator
    {
        List<object[]> MDGetCommandsForNewPlayer();

        List<object[]> MDGetCommands();

        void MDProcessCommand(params object[] Parameters);

        void MDSetSettings(MDReplicatedSetting[] Settings);
    }

    public class MDReplicatedCommandReplicator : MDReplicatedMember
    {
        private const String REPLICATE_METHOD_NAME = "ReplicateClockedValues";

        public enum Settings
        {
            Converter,
            OnValueChangedEvent
        }

        protected MethodInfo OnValueChangedCallback = null;

        protected MDReplicator Replicator;

        protected MDGameSession GameSession;

        public MDReplicatedCommandReplicator(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef, MDReplicatedSetting[] Settings) 
                                        : base(Member, true, ReplicatedType, NodeRef, Settings) 
        {
            GameSession = MDStatics.GetGameSession();
            Replicator = GameSession.Replicator;
            Node node = NodeRef.GetRef() as Node;
            IMDCommandReplicator CommandReplicator = InitializeCommandReplicator(Member, node);
            ParseSettings(MDReplicator.ParseParameters(typeof(Settings), Settings));
            CommandReplicator.MDSetSettings(Settings);
        }

        protected void ParseSettings(MDReplicatedSetting[] Settings)
        {
            foreach (MDReplicatedSetting setting in Settings)
            {
                switch ((Settings)setting.Key)
                {
                    case MDReplicatedCommandReplicator.Settings.OnValueChangedEvent:
                        Node Node = NodeRef.GetRef() as Node;
                        OnValueChangedCallback = Node.GetType().GetMethod(setting.Value.ToString(),
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                        break;
                }
            }
        }

        public override void SetValues(uint Tick, params object[] Parameters)
        {
            GetCommandReplicator().MDProcessCommand(Parameters);
            if (OnValueChangedCallback != null)
            {
                Node Instance = NodeRef.GetRef() as Node;
                OnValueChangedCallback.Invoke(Instance, null);
            }
        }

        private IMDCommandReplicator GetCommandReplicator()
        {
            return (IMDCommandReplicator)GetValue();
        }

        public override void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
        {
            IMDCommandReplicator CommandReplicator = GetCommandReplicator();
            Node Instance = NodeRef.GetRef() as Node;

            if (CommandReplicator == null)
            {
                MDLog.Error("Command replicator is null for member {0}#{1}", Instance.GetPath(), Member.Name);
                return;
            }

            if ((GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime) || (GetReplicatedType() == MDReplicatedType.OnChange))
            {
                List<object[]> commands = GetCommandReplicator().MDGetCommands();
                if (commands.Count > 0)
                {
                    // Do replication to all except joining peer if we got one
                    commands.ForEach(value =>
                    {
                        foreach (int PeerId in GameSession.GetAllPeerIds())
                        {
                            if (PeerId != JoinInProgressPeerId && PeerId != MDStatics.GetPeerId())
                            {
                                ReplicateCommandToPeer(value, PeerId);
                            }
                        }

                    });
                }
            }

            if (JoinInProgressPeerId != -1)
            {
                // Replicate current data to joining peer and send current command we are at
                List<object[]> newPlayerCommands = CommandReplicator.MDGetCommandsForNewPlayer();
                newPlayerCommands.ForEach(value => ReplicateCommandToPeer(value, JoinInProgressPeerId));
            }
        }

        protected void ReplicateCommandToPeer(object[] Command, int PeerId)
        {
            Replicator.RpcId(PeerId, REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), 0, Command);
        }


        private IMDCommandReplicator InitializeCommandReplicator(MemberInfo Member, Node Node)
        {
            if (GetValue() != null)
            {
                return GetCommandReplicator();
            }

            Type MemberType = Member.GetUnderlyingType();
            if (MemberType != null && MemberType.GetInterface(nameof(IMDCommandReplicator)) != null)
            {
                IMDCommandReplicator CommandReplicator = Activator.CreateInstance(MemberType) as IMDCommandReplicator;
                Member.SetValue(Node, CommandReplicator);
                return CommandReplicator;
            }

            return null;
        }    
    }
}