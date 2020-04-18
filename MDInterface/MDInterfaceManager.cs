using Godot;
using System;

/*
 * MDInterfaceManager
 *
 * Class that manages all our UI.
 */
public class MDInterfaceManager : CanvasLayer
{
    private const int ConsoleKey = (int)KeyList.Quoteleft;
    private const string ConsoleName = "Console";
    private const string LOG_CAT = "InterfaceManager";

    public override void _Ready()
    {
        SetProcessInput(true);
    }

    public override void _Input(InputEvent InEvent)
    {
        if (this.GetGameInstance().IsConsoleAvailable())
        {
            if (InEvent is InputEventKey EventKey)
            {
                if (EventKey.Pressed && !EventKey.Echo && EventKey.Scancode == ConsoleKey)
                {
                    ToggleConsole();
                    this.SetInputHandled();
                }
            }
        }
    }

    // Opens and focuses the console UI
    private void ToggleConsole()
    {
        MDConsole Console = FindNode(ConsoleName, true, false) as MDConsole;
        if (Console == null)
        {
            Console = new MDConsole();
            Console.Name = ConsoleName;
            AddChild(Console);
        }
        else
        {
            Console.Close();
        }
    }
}