using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

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
        Converter
    }

    protected MDReplicator Replicator;

    protected MDGameSession GameSession;

    protected IMDCommandReplicator CommandReplicator;

    public MDReplicatedCommandReplicator(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef, MDReplicatedSetting[] Settings) 
                                    : base(Member, true, ReplicatedType, NodeRef, Settings) 
    {
        GameSession = MDStatics.GetGameSession();
        Replicator = GameSession.Replicator;
        Node node = NodeRef.GetRef() as Node;
        InitializeCommandReplicator(Member, node);
        CommandReplicator.MDSetSettings(Settings);
    }

    public override void SetValues(uint Tick, params object[] Parameters)
    {
        CommandReplicator.MDProcessCommand(Parameters);
    }

    public override void Replicate(int JoinInProgressPeerId, bool IsIntervalReplicationTime)
    {
        Node Instance = NodeRef.GetRef() as Node;

        if ((GetReplicatedType() == MDReplicatedType.Interval && IsIntervalReplicationTime) || (GetReplicatedType() == MDReplicatedType.OnChange))
        {
            List<object[]> commands = CommandReplicator.MDGetCommands();
            if (commands.Count > 0)
            {
                // Do replication to all except joining peer if we got one
                commands.ForEach(value =>
                {
                    foreach (int PeerId in GameSession.GetAllPeerIds())
                    {
                        if (PeerId != JoinInProgressPeerId)
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


    private void InitializeCommandReplicator(MemberInfo Member, Node Node)
    {
        Type MemberType = Member.GetType();
        if (MemberType != null && MemberType.IsAssignableFrom(typeof(IMDCommandReplicator)))
        {
            CommandReplicator = Activator.CreateInstance(MemberType) as IMDCommandReplicator;
        }
        Member.SetValue(Node, CommandReplicator);
    }    
}
