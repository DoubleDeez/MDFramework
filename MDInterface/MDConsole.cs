using Godot;
using System;
using CmdList = System.Collections.Generic.List<string>;

/*
 * MDConsole
 *
 * Class that allows the user to enter console commands that have been registered with MDCommand
 */
public class MDConsole : Panel
{
    public override void _Ready()
    {
        base._Ready();
        
        this.SetAnchor(0, 1, 1, 1);
        this.SetMargin(0, -24, 0, 0);

        CommandHistory = MDCommands.GetCommandHistory();

        CreateLineEdit();

        SetProcessInput(true);
    }

    public override void _Input(InputEvent InEvent)
    {
        if (InEvent is InputEventKey EventKey)
        {
            if (EventKey.Pressed && !EventKey.Echo)
            {
                if ( EventKey.GetScancode() == (int)KeyList.Up)
                {
                    NavigateHistory(1);
                    this.SetInputHandled();
                }
                else if ( EventKey.GetScancode() == (int)KeyList.Down)
                {
                    NavigateHistory(-1);
                    this.SetInputHandled();
                }
            }
        }
    }

    // Closes and frees the console prompt
    public void Close()
    {
        GetParent().RemoveChild(this);
        QueueFree();
    }

    // Navigates up/down the command history
    private void NavigateHistory(int Direction)
    {
        int HistoryCount = CommandHistory.Count;
        if (HistoryCount == 0)
        {
            return;
        }

        if (CmdHistoryIndex == -1)
        {
            StoredCommand = ConsoleInput.GetText();
        }

        CmdHistoryIndex = Mathf.Clamp(CmdHistoryIndex + Direction, -1, HistoryCount - 1);

        if (CmdHistoryIndex == -1)
        {
            ConsoleInput.SetText(StoredCommand);
        }
        else
        {
            ConsoleInput.SetText(CommandHistory[CmdHistoryIndex]);
        }
    }

    // Creates the UI control that accepts text input
    private void CreateLineEdit()
    {
        ConsoleInput = new LineEdit();
        ConsoleInput.SetName("ConsoleInput");
        AddChild(ConsoleInput);

        ConsoleInput.SetAnchor(0, 0, 1, 1);
        ConsoleInput.SetMargin(0);
        ConsoleInput.SetContextMenuEnabled(false);
        ConsoleInput.Connect("text_entered", this, "OnCommandEntered");
        ConsoleInput.GrabFocus();
    }

    // Called when the user hits enter on the ConsoleInput
    private void OnCommandEntered(string Command)
    {
        MDCommands.InvokeCommand(Command);
        Close();
    }

    private LineEdit ConsoleInput;
    private CmdList CommandHistory;
    private int CmdHistoryIndex = -1;
    private string StoredCommand = "";
}