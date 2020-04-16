using System;

public enum MDReliability
{
    Reliable,
    Unreliable
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MDReplicated : Attribute
{
    public MDReplicated(MDReliability InReliability = MDReliability.Reliable)
    {
        Reliability = InReliability;
    }

    public MDReliability Reliability {private set; get;}
}