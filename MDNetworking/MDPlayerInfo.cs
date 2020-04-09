using Godot;
using System;

/*
 * MDPlayerInfo
 *
 * Class that tracks the players game data that should be replicated to other players
 */
public class MDPlayerInfo : Node
{
    private const string LOG_CAT = "LogPlayerInfo";

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