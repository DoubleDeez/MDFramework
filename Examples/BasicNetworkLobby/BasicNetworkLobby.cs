using Godot;
using System;
using MD;

/*
    Very simple example on how you can host / join games
*/
[MDAutoRegister]
public class BasicNetworkLobby : Node2D
{
    private const string LOG_CAT = "LogBasicNetworkLobby";

    private const string TEXT_JOIN_SERVER = "Join Server";

    private const string TEXT_CONNECTING = "Connecting...";

    [MDBindNode("CanvasLayer/CenterContainer/ParentGrid/CenterContainer/GridContainer/GridContainer/TextAddress")]
    protected TextEdit TextHost;

    [MDBindNode("CanvasLayer/CenterContainer/ParentGrid/CenterContainer/GridContainer/GridContainer/TextPort")]
    protected TextEdit TextPort;

    [MDBindNode("CanvasLayer/CenterContainer/ParentGrid/CenterContainer/GridContainer/BtnJoin")]
    protected Button ButtonJoin;

    [MDBindNode("CanvasLayer/CenterContainer/ParentGrid/CenterContainer/GridContainer/BtnHost")]
    protected Button ButtonHost;

    [MDBindNode("CanvasLayer/CenterContainer/ParentGrid/CenterContainer/GridContainer/BtnSinglePlayer")]
    protected Button ButtonSinglePlayer;

    [MDBindNode("CanvasLayer/BtnDisconnect")]
    protected Button ButtonDisconnect;


    [MDBindNode("CanvasLayer/CenterContainer")]
    protected Control InterfaceRoot;

    protected MDGameSession GameSession;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GameSession = this.GetGameSession();
        GameSession.OnPlayerJoinedEvent += OnPlayerJoined;
        GameSession.OnPlayerLeftEvent += OnPlayerLeft;
        GameSession.OnPlayerNameChanged += OnPlayerNameChanged;
        GameSession.OnSessionStartedEvent += OnSessionStartedEvent;
        GameSession.OnSessionFailedEvent += OnSessionFailedOrEndedEvent;
        GameSession.OnSessionEndedEvent += OnSessionFailedOrEndedEvent;
        ToggleDisconnectVisible(false);
    }

    public override void _ExitTree()
    {
        GameSession.OnPlayerJoinedEvent -= OnPlayerJoined;
        GameSession.OnPlayerLeftEvent -= OnPlayerLeft;
        GameSession.OnPlayerNameChanged -= OnPlayerNameChanged;
        GameSession.OnSessionStartedEvent -= OnSessionStartedEvent;
        GameSession.OnSessionFailedEvent -= OnSessionFailedOrEndedEvent;
        GameSession.OnSessionEndedEvent += OnSessionFailedOrEndedEvent;
    }

    #region USER INPTU

    protected virtual void OnHostPressed()
    {
        GameSession.StartServer(GetPort());
    }

    private void OnDisconnectPressed()
    {
        if (GameSession.IsSessionStarted)
        {
            GameSession.Disconnect();
        }
    }

    protected virtual void OnJoinPressed()
    {
        // Attempt to connect as client
        if (GameSession.StartClient(GetHost(), GetPort()))
        {
            // Disable buttons while we try to join
            ToggleButtons(false);
            SetJoinButtonText(TEXT_CONNECTING);
        }
    }

    protected virtual void OnSinglePlayerPressed()
    {
        // Start a single player game
        GameSession.StartStandalone();
    }

    #endregion

    #region EVENTS

    protected virtual void OnSessionStartedEvent()
    {
        ToggleInterface(false);
        ToggleDisconnectVisible(true);
    }

    protected virtual void OnSessionFailedOrEndedEvent()
    {
        ToggleButtons(true);
        ToggleInterface(true);
        SetJoinButtonText(TEXT_JOIN_SERVER);
        ToggleDisconnectVisible(false);
    }

    protected virtual void OnPlayerNameChanged(int PeerId)
    {
        MDLog.Info(LOG_CAT, $"Player changed name to {GameSession.GetPlayerInfo(PeerId).GetPlayerName()}");
    }

    protected virtual void OnPlayerLeft(int PeerId)
    {
        // TODO: Do cleanup code here
        // Note: You can't access PlayerInfo here, to access that override PreparePlayerInfoForRemoval in GameSession.
        MDLog.Info(LOG_CAT, $"Player left with PeerID {PeerId}");
    }

    protected virtual void OnPlayerJoined(int PeerId)
    {
        // TODO: Spawn player here, should be done with CallDeferred
        MDLog.Info(LOG_CAT,
            $"Player joined {GameSession.GetPlayerInfo(PeerId).GetPlayerName()} with PeerID {PeerId}");
    }

    #endregion

    #region SUPPORT METHODS

    protected void SetJoinButtonText(String text)
    {
        if (ButtonJoin == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find join button");
            return;
        }

        ButtonJoin.Text = text;
    }

    protected void ToggleInterface(bool visible)
    {
        if (InterfaceRoot == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find interface root");
            return;
        }

        InterfaceRoot.Visible = visible;
    }

    protected void ToggleDisconnectVisible(bool visible)
    {
        if (ButtonDisconnect == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find disconnect button");
            return;
        }

        ButtonDisconnect.Visible = visible;
    }


    private void ToggleButtons(bool Enabled)
    {
        ToggleButton(ButtonHost, Enabled);
        ToggleButton(ButtonJoin, Enabled);
        ToggleButton(ButtonSinglePlayer, Enabled);
    }

    private void ToggleButton(Button Button, bool Enabled)
    {
        if (Button == null)
        {
            MDLog.Warn(LOG_CAT, "A button was null");
            return;
        }

        Button.Disabled = !Enabled;
    }

    private String GetHost()
    {
        if (TextHost == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find host textbox");
            return "127.0.0.1";
        }

        return TextHost.Text;
    }

    private int GetPort()
    {
        if (TextPort == null)
        {
            MDLog.Warn(LOG_CAT, "Could not find port textbox");
            return 1234;
        }

        return Int32.Parse(TextPort.Text);
    }

    #endregion
}