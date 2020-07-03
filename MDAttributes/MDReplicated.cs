using System;

public enum MDReliability
{
    Reliable,
    Unreliable
}

public enum MDReplicatedType
{
    OnChange, // Replicates the value when the value has changed
    Interval, // Replicates the value at a regular interval which can be defined in the MDReplicator
    JoinInProgress // Replicates the value to players that join during a session
    // TODO - Add Phased
    // TODO - Add Interpolated
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MDReplicated : Attribute
{
    public MDReplicated(MDReliability InReliability = MDReliability.Reliable,
        MDReplicatedType RepType = MDReplicatedType.OnChange)
    {
        Reliability = InReliability;
        ReplicatedType = RepType;
    }

    public MDReliability Reliability { private set; get; }
    public MDReplicatedType ReplicatedType { private set; get; }
}