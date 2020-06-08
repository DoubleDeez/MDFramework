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
    private String _playerName = "UnkownPlayer";
    private const string LOG_CAT = "LogPlayerInfo";

    [MDReplicated]
    public string PlayerName
    {
        get { return _playerName; }
        set {
            _playerName = value;
            this.GetGameSession().NotifyPlayerNameChanged(PeerId);
        }
   }

    public void InitPlayerInfo(int PlayerPeerId)
    {
        PeerId = PlayerPeerId;
        Name = PeerId.ToString();
        SetNetworkMaster(PeerId);
    }

    public virtual void PerformFullSync(int PeerId)
    {

    }


    public int PeerId {get; private set;}
}
