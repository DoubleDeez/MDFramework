using System;

public enum RPCType
{
    Client, // Server sending RPC to client net owner
    Server, // Client net owner sending RPC to server
    Broadcast, // Server sending RPC to all clients
}

public enum RPCReliability
{
    Reliable, // Guaranteed to arrive, in order
    Unreliable, // May not arrive, but will be in order
    Unsequenced, // May not arrive and maybe not in order
}

[AttributeUsage(AttributeTargets.Method)]
public class MDRpc : Attribute
{
    MDRpc(RPCType InType, RPCReliability InReliability)
    {
        Type = InType;
        Reliability = InReliability;
    }

    public RPCType Type {get;set;}
    public RPCReliability Reliability {get;set;}
}