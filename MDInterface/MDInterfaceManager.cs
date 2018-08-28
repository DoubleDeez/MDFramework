using Godot;
using System;

/*
 * MDInterfaceManager
 *
 * Class that manages all our UI.
 */
public class MDInterfaceManager : Control
{
    private const int ConsoleKey = (int)KeyList.Quoteleft;
    private const string LOG_CAT = "InterfaceManager";

    public override void _Ready()
    {
        base._Ready();

        this.SetAnchor(0, 0, 1, 1);
        this.SetMargin(0);

        SetProcessInput(true);

        MDLog.Log(LOG_CAT, MDLogLevel.Info, "Interface manager ready");
    }

    public override void _Input(InputEvent InEvent)
    {
        if (InEvent is InputEventKey EventKey)
        {
            if (EventKey.Pressed && !EventKey.Echo && EventKey.GetScancode() == ConsoleKey)
            {
                ToggleConsole();
                this.SetInputHandled();
            }
        }
    }

    // Opens and focuses the console UI
    private void ToggleConsole()
    {
        if (Console == null)
        {
            Console = new MDConsole();
            Console.SetName("Console");
            Console.RequestClose = ToggleConsole;
            AddChild(Console);
        }
        else
        {
            RemoveChild(Console);
            Console.QueueFree();
            Console = null;
        }
    }

    private MDConsole Console;
}