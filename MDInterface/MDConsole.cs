using Godot;
using System;

/*
 * MDConsole
 *
 * Class that allows the user to enter console commands that have been registered with MDCommand
 */
public class MDConsole : Panel
{
    public delegate void OnCloseRequested();
    public OnCloseRequested RequestClose = null;

    public override void _Ready()
    {
        base._Ready();
        
        this.SetAnchor(0, 1, 1, 1);
        this.SetMargin(0, -24, 0, 0);

        CreateLineEdit();
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
        if (RequestClose != null)
        {
            RequestClose.Invoke();
        }
    }

    private LineEdit ConsoleInput;
}