using Godot;
using System;

/*
 * MDPlayerInfo
 *
 * Class that tracks the players game data that should be replicated to other players
 */
 [MDAutoRegister]
public class MDPlayerInfo : Node
{
    private const string LOG_CAT = "LogPlayerInfo";
    private String _playerName = "";

    public void InitPlayerInfo(int PlayerPeerId)
    {
        PeerId = PlayerPeerId;
        Name = PeerId.ToString();
        SetNetworkMaster(PeerId);
        if (PeerId == MDStatics.GetPeerId())
        {
            SetPlayerName(GetPlayerNameInt());
        }
        else
        {
            // Set a default name for other peers
            _playerName = "Peer" + PeerId;
        }
    }

    ///<summary>Override this to set the player name when the player info is initialized</summary>
    protected virtual String GetPlayerNameInt()
    {
        return "Player" + PeerId;
    }

    public void SetPlayerName(String name)
    {
        SetPlayerNameInt(name);
    }

    ///<summary> Returns the name of this player</summary>
    public String GetPlayerName()
    {
        return _playerName;
    }

    ///<summary>Called when a new player joins, used to synchronize the new player
    ///<para>PeerId is the ID of the new player that joined</para></summary>
    ///<param name="PeerId">The ID of the new player that joined</param>
    public virtual void PerformFullSync(int PeerId)
    {
        // Sync the new peer
        RpcId(PeerId, nameof(SetPlayerNameInt), GetPlayerName());
    }


    [Puppet]
    protected void SetPlayerNameInt(String name)
    {
        _playerName = name;
        MDStatics.GetGameSession().NotifyPlayerNameChanged(PeerId);
        if (PeerId == MDStatics.GetPeerId())
        {
            // Notify other clients
            Rpc(nameof(SetPlayerNameInt), name);
        }
    }


    public int PeerId {get; private set;}
}
