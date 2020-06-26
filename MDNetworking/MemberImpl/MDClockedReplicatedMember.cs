using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public class MDClockedReplicatedMember : MDReplicatedMember
{
    private const String REPLICATE_METHOD_NAME = "ReplicateClockedValue";

    protected SortedDictionary<uint, object> ValueList = new SortedDictionary<uint, object>();

    protected MDGameClock GameClock;

    protected MDReplicator Replicator;

    protected uint LastTickValueWasChanged = 0;

    public MDClockedReplicatedMember(MemberInfo Member, bool Reliable, MDReplicatedType ReplicatedType, WeakRef NodeRef) 
                                    : base(Member, Reliable, ReplicatedType, NodeRef) 
    {
        GameClock = MDStatics.GetGameSynchronizer().GameClock;
        Replicator = MDStatics.GetGameSession().Replicator;
    }

    public override void SetValue(uint Tick, object Value)
    {
        // Could be overwritten for a more optional conversion
        if (!ValueList.ContainsKey(Tick))
        {
            ValueList.Add(Tick, Value);
        }
    }

    public override void CheckForValueUpdate()
    {
        // Check if we are the owner of this
        if (ShouldReplicate())
        {
            return;
        }

        // Find the most recent update
        List<uint> touchedKeys = new List<uint>();
        uint foundKey = 0;
        foreach (uint key in ValueList.Keys)
        {
            if (key > GameClock.GetRemoteTick())
            {
                break;
            }
            touchedKeys.Add(key);
            foundKey = key;
        }

        // Didn't find any updates
        if (foundKey == 0)
        {
            return;
        }

        // Set the value
        UpdateValue(ValueList[foundKey]);

        // Remove old
        touchedKeys.ForEach((k) => ValueList.Remove(k));
    }

    protected void UpdateValue(object value)
    {
        Node Instance = NodeRef.GetRef() as Node;
        GetPropertyInfo().SetValue(Instance, value, null);
    }

    ///<summary>Replicate this value to all clients</summary>
    protected override void ReplicateToAll(Node Node, object Value)
    {
        MDLog.Debug(LOG_CAT, "Replicating {0} with value {1} from {2}", Member.Name, Value, LastValue);
        if (Reliable)
        {
            Replicator.Rpc(REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), GameClock.GetTick(), Value);
        }
        else
        {
            Replicator.RpcUnreliable(REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), GameClock.GetTick(), Value);
        }
        LastValue = Value;
    }

    ///<summary>Replicate this value to the given peer</summary>
    protected override void ReplicateToPeer(Node Node, object Value, int PeerId)
    {
        MDLog.Debug(LOG_CAT, "Replicating to JIP Peer {0} for member {1} with value {2}", PeerId, Member.Name, Value);
        if (Reliable)
        {
            Replicator.RpcId(PeerId, REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), GameClock.GetTick(), Value);
        }
        else
        {
            Replicator.RpcUnreliableId(PeerId, REPLICATE_METHOD_NAME, Replicator.GetReplicationIdForKey(GetUniqueKey()), GameClock.GetTick(), Value);
        }
    }


}
