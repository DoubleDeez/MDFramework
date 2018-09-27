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
    private const string ConsoleName = "Console";
    private const string LOG_CAT = "InterfaceManager";

    public override void _Ready()
    {
        this.SetAnchor(0, 0, 1, 1);
        this.SetMargin(0);

        SetProcessInput(true);
    }

    public override void _Input(InputEvent InEvent)
    {
        #if DEBUG
        if (InEvent is InputEventKey EventKey)
        {
            if (EventKey.Pressed && !EventKey.Echo && EventKey.GetScancode() == ConsoleKey)
            {
                ToggleConsole();
                this.SetInputHandled();
            }
        }
        #endif
    }

    // Opens and focuses the console UI
    private void ToggleConsole()
    {
        MDConsole Console = FindNode(ConsoleName, true, false) as MDConsole;
        if (Console == null)
        {
            Console = new MDConsole();
            Console.SetName(ConsoleName);
            AddChild(Console);
        }
        else
        {
            Console.Close();
        }
    }
}