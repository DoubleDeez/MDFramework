using Godot;
using System;
using MD;

[MDAutoRegister]
public class PeerSynchStatusRow : ColorRect
{
    private static readonly String LOG_CAT = "LogPeerSynchStatusRow";

    [MDBindNode("GridContainer/LabelName")]
    protected Label PlayerName;

    [MDBindNode("GridContainer/ProgressBar")]
    protected ProgressBar ProgressBar;

    public int PeerId { get; set; } = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    public void SetPlayerName(String name)
    {
        if (PlayerName == null)
        {
            MDLog.Error(LOG_CAT, "PlayerName label not found");
            return;
        }

        PlayerName.Text = name;
    }

    ///<summary>Expects percentage to be from 0 to 1</summary>
    public void SetProgressPercentage(float percentage)
    {
        if (ProgressBar == null)
        {
            MDLog.Error(LOG_CAT, "ProgressBar not found");
            return;
        }

        ProgressBar.Value = percentage * 100;
    }
}