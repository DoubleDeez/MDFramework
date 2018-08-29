using Godot;
using System;

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

        CreateLineEdit();
    }

    // Closes and frees the console prompt
    public void Close()
    {
        GetParent().RemoveChild(this);
        QueueFree();
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
        MDCommand.InvokeCommand(Command);
        Close();
    }

    private LineEdit ConsoleInput;
}