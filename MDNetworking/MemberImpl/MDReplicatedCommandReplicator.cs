using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MD
{
    ///<summary>The command replicator can replicate custom commands across the network</summary>
    public interface IMDCommandReplicator
    {
        /// <summary>
        /// Get a list of commands for a player that just joined so they can synch up to the current state.
        /// </summary>
        /// <returns>List of commands</returns>
        List<object[]> MDGetCommandsForNewPlayer();

        /// <summary>
        /// Get a list of commands containing changes since the last update.
        /// </summary>
        /// <returns>List of commands</returns>
        List<object[]> MDGetCommands();

        /// <summary>
        /// Process a command that we received.
        /// </summary>
        /// <param name="Parameters">The command parameters</param>
        void MDProcessCommand(params object[] Parameters);

        /// <summary>
        /// Prase the settings that was set as attributes, remember to filter for the ones you care about.
        /// </summary>
        /// <param name="Settings">List of settings</param>
        void MDSetSettings(MDReplicatedSetting[] Settings);

        /// <summary>
        /// Check if there are any commands in the queue or if any subclass has changes
        /// </summary>
        /// <returns>Returns true if it should, false if not</returns>
        bool MDShouldBeReplicated();

        void MDDoFullResynch(object list);
    }

    /// <summary>
    /// A command replicator capable of replicating any class that implements IMDCommandReplicator across the network
    /// </summary>
    public class MDReplicatedCommandReplicator : MDReplicatedMember
    {
        public MDReplicatedCommandReplicator(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef, MDReplicatedSetting[] Settings) 
                                        : base(Member, true, ReplicatedType, NodeRef, Settings) 
        {
            GameSession = MDStatics.GetGameSession();
            Replicator = GameSession.Replicator;
            GameClock = GameSession.GetGameClock();
            Node node = NodeRef.GetRef() as Node;
            IMDCommandReplicator CommandReplicator = InitializeCommandReplicator(Member, node);
            CommandReplicator.MDSetSettings(Settings);
        }

        public override void SetValues(uint Tick, params object[] Parameters)
        {
            // If the tick this update is for is past the current tick
            if (GameClock.GetRemoteTick() >= Tick || IsSynchInProgress())
            {
                GetCommandReplicator().MDProcessCommand(Parameters);
                if (OnValueChangedCallback != null)
                {
                    Node Instance = NodeRef.GetRef() as Node;
                    OnValueChangedCallback.Invoke(Instance, null);
                }
            }
            else
            {
                if (!ValueList.ContainsKey(Tick))
                {
                    ValueList.Add(Tick, new List<object>());
                }

                ValueList[Tick].Add(Parameters);
            }
        }

        public override void CheckForValueUpdate()
        {
            // Check if we are the owner of this
            if (ShouldReplicate())
            {
                return;
            }

            uint RemoteTick = GameClock.GetRemoteTick();
            bool ValueChanged = false;

            // Find the most recent update
            List<uint> touchedKeys = new List<uint>();
            foreach (uint key in ValueList.Keys)
            {
                if (key > RemoteTick)
                {
                    break;
                }

                ValueList[key].ForEach(parameters => GetCommandReplicator().MDProcessCommand((object[])parameters));
                ValueChanged = true;
                touchedKeys.Add(key);
            }

            if (ValueChanged)
            {
                // Remove old
                touchedKeys.ForEach(k => ValueList.Remove(k));

                // Send event
                if (OnValueChangedCallback != null)
                {
                    Node Instance = NodeRef.GetRef() as Node;
                    OnValueChangedCallback.Invoke(Instance, null);
                }
            }
        }

        private IMDCommandReplicator GetCommandReplicator()
        {
            Node node = NodeRef.GetRef() as Node;
            return (IMDCommandReplicator)Member.GetValue(node);
        }

        public override void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
        {
            IMDCommandReplicator CommandReplicator = GetCommandReplicator();
            Node Instance = NodeRef.GetRef() as Node;

            if (CommandReplicator == null)
            {
                MDLog.Error(LOG_CAT, $"Command replicator is null for member {Instance.GetPath()}#{Member.Name}");
                return;
            }

            if ((GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime) || (GetReplicatedType() == MDReplicatedType.OnChange))
            {
                // We do a check here to see if anything has updated
                GetCommandReplicator().MDShouldBeReplicated();
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

        private void ReplicateCommandToPeer(object[] Command, int PeerId)
        {
            Replicator.RpcId(PeerId, MDReplicator.REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), GetGameTick(), Command);
        }

        private IMDCommandReplicator InitializeCommandReplicator(MemberInfo Member, Node Node)
        {
            if (Member.GetValue(Node) != null)
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