using Godot;
using System;

/*
 * MDPlayer
 *
 * Class that tracks the players game data, to be replicated to relevant players.
 */
public class MDPlayer : Node
{
    private const string LOG_CAT = "LogPlayer";

    public override void _Ready()
    {
        this.CallRPC(nameof(ServerSetPlayerName), GetName());
    }

    [MDRpc(RPCType.Server, RPCReliability.Reliable)]
    public void ServerSetPlayerName(string Name)
    {
        PlayerName = Name;
        MDLog.Info(LOG_CAT, "Test Server RPC {0}", Name);
        this.CallRPC(nameof(ClientSetPlayerName), Name);
    }

    [MDRpc(RPCType.Client, RPCReliability.Reliable)]
    public void ClientSetPlayerName(string Name)
    {
        MDLog.Info(LOG_CAT, "Test Client RPC {0}", Name);
    }

    [MDReplicated()]
    public string PlayerName;

    public int PeerID {get; set;}
}
