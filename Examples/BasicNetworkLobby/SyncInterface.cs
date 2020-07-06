using Godot;
using MD;

[MDAutoRegister]
public class SyncInterface : CenterContainer
{
    protected PackedScene SynchRow = null;

    [MDBindNode("GridContainer/Header")]
    protected Control Header;

    [MDBindNode("GridContainer")]
    protected GridContainer Container;

    [MDBindNode("ResumeSquare")]
    protected Control ResumeBox;

    [MDBindNode("ResumeTimer")]
    protected Timer ResumeTimer;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Visible = false;
        this.GetGameSynchronizer().OnSynchStartedEvent += OnSynchStartedEvent;
        this.GetGameSynchronizer().OnPlayerSynchStatusUpdateEvent += OnPlayerSynchStatusUpdateEvent;
        this.GetGameSynchronizer().OnSynchCompleteEvent += OnSynchCompleteEvent;
        this.GetGameSession().OnPlayerJoinedEvent += OnPlayerJoinedEvent;
        this.GetGameSession().OnPlayerLeftEvent += OnPlayerLeftEvent;
        this.GetGameSession().OnPlayerNameChanged += OnPlayerNameChanged;

        ResumeTimer.OneShot = true;
        ResumeTimer.PauseMode = PauseModeEnum.Process;
    }

    public override void _ExitTree()
    {
        this.GetGameSynchronizer().OnSynchStartedEvent -= OnSynchStartedEvent;
        this.GetGameSynchronizer().OnPlayerSynchStatusUpdateEvent -= OnPlayerSynchStatusUpdateEvent;
        this.GetGameSynchronizer().OnSynchCompleteEvent -= OnSynchCompleteEvent;
        this.GetGameSession().OnPlayerJoinedEvent -= OnPlayerJoinedEvent;
        this.GetGameSession().OnPlayerLeftEvent -= OnPlayerLeftEvent;
        this.GetGameSession().OnPlayerNameChanged -= OnPlayerNameChanged;
    }

    protected void OnPlayerJoinedEvent(int PeerId)
    {
        if (Visible)
        {
            MDPlayerInfo info = this.GetGameSession().GetPlayerInfo(PeerId);
            NewSynchRow(info);
        }
    }

    protected void OnPlayerLeftEvent(int PeerId)
    {
        if (Visible)
        {
            PeerSynchStatusRow row = FindPeerRow(PeerId);
            if (row != null)
            {
                row.RemoveAndFree();
            }
        }
    }

    protected void OnPlayerNameChanged(int PeerId)
    {
        PeerSynchStatusRow row = FindPeerRow(PeerId);
        if (row != null)
        {
            MDPlayerInfo info = this.GetGameSession().GetPlayerInfo(row.PeerId);
            row.SetPlayerName(info.GetPlayerName());
        }
    }

    protected void InitPeerList()
    {
        ClearPeerList();
        foreach (MDPlayerInfo info in this.GetGameSession().GetAllPlayerInfos())
        {
            NewSynchRow(info);
        }
    }

    protected void ClearPeerList()
    {
        foreach (Control control in Container.GetChildren())
        {
            if (control != Header)
            {
                control.RemoveAndFree();
            }
        }
    }

    protected void OnSynchStartedEvent(bool IsPaused)
    {
        Visible = true;
        ResumeBox.Visible = false;
        ResumeTimer.Stop();
        InitPeerList();
    }

    protected void OnPlayerSynchStatusUpdateEvent(int PeerId, float ProgressPercentage)
    {
        PeerSynchStatusRow row = FindPeerRow(PeerId);
        if (row != null)
        {
            row.SetProgressPercentage(ProgressPercentage);
        }
    }

    protected void OnSynchCompleteEvent(float ResumeGameIn)
    {
        ResumeBox.Visible = true;
        ResumeTimer.Start(ResumeGameIn);
    }

    private void OnResumeTimerTimeout()
    {
        Visible = false;
    }

    private PeerSynchStatusRow FindPeerRow(int PeerId)
    {
        foreach (Control control in Container.GetChildren())
        {
            if (control is PeerSynchStatusRow && ((PeerSynchStatusRow) control).PeerId == PeerId)
            {
                return (PeerSynchStatusRow) control;
            }
        }

        return null;
    }

    private void NewSynchRow(MDPlayerInfo info)
    {
        // This is to avoid needing references
        if (SynchRow == null)
        {
            SynchRow = (PackedScene) ResourceLoader.Load(Filename.GetBaseDir() + "/PeerSynchStatusRow.tscn");
        }

        PeerSynchStatusRow row = (PeerSynchStatusRow) SynchRow.Instance();
        Container.AddChild(row);
        row.SetPlayerName(info.GetPlayerName());
        row.PeerId = info.PeerId;
        if (info.PeerId == MDStatics.GetServerId())
        {
            row.SetProgressPercentage(1);
        }
    }
}