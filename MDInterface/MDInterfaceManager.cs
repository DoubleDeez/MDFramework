using Godot;
using System;

namespace MD
{
/*
 * MDInterfaceManager
 *
 * Class that manages all our UI.
 */
    public class MDInterfaceManager : CanvasLayer
    {
        private const string ConsoleName = "Console";
        private const string OnScreenDebugName = "OnScreenDebug";
        private const string LOG_CAT = "InterfaceManager";

        public override void _Ready()
        {
            SetProcessInput(true);
        }

        public override void _Input(InputEvent InEvent)
        {
            if (InEvent is InputEventKey EventKey && EventKey.Pressed && !EventKey.Echo)
            {
                if (this.GetGameInstance().IsConsoleAvailable() &&
                    EventKey.Scancode == this.GetGameInstance().GetConsoleKey())
                {
                    ToggleConsole();
                    this.SetInputHandled();
                }

                if (this.GetGameInstance().IsOnScreenDebugAvailable() &&
                    EventKey.Scancode == this.GetGameInstance().GetOnScreenDebugKey())
                {
                    ToggleOnScreenDebug();
                    this.SetInputHandled();
                }
            }
        }

        // Opens and focuses the console UI
        private void ToggleConsole()
        {
            MDConsole Console = FindNode(ConsoleName, true, false) as MDConsole;
            if (Console == null)
            {
                Console = new MDConsole
                {
                    Name = ConsoleName
                };
                AddChild(Console);
            }
            else
            {
                Console.Close();
            }
        }

        private void ToggleOnScreenDebug()
        {
            MDOnScreenDebug OnScreenDebug = FindNode(OnScreenDebugName, true, false) as MDOnScreenDebug;
            if (OnScreenDebug == null)
            {
                OnScreenDebug = new MDOnScreenDebug
                {
                    Name = OnScreenDebugName
                };
                AddChild(OnScreenDebug);
            }
            else
            {
                OnScreenDebug.Close();
            }
        }
    }
}