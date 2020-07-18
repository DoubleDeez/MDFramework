using Godot;
using System;
using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// Identifiers for each screen layer in the interface manager
    /// </summary>
    public enum MDScreenLayer
    {
        /// <summary>The 'lowest' layer, only the top screen in the <c>Primary</c> stack is visible</summary>
        Primary,
        /// <summary>Displays on top of the <c>Primary</c> layer, all screens in the <c>PopUp</c> stack are visible</summary>
        PopUp,
        /// <summary>Debug features are render in this layer</summary>
        Debug,
        /// <summary>The console renders in this layer to be on top of everything</summary>
        Console
    }

    
    /// <summary>
    /// Class that manages all our UI.
    /// </summary>
    public class MDInterfaceManager : CanvasLayer
    {
        private const string ConsoleName = "Console";
        private const string OnScreenDebugName = "OnScreenDebug";
        private const string LOG_CAT = "InterfaceManager";
        private Dictionary<MDScreenLayer, MDLayerStack> LayerMap = new Dictionary<MDScreenLayer, MDLayerStack>();
        private MDConsole Console;
        private MDOnScreenDebug OnScreenDebug;

        public override void _Ready()
        {
            SetProcessInput(true);
            ConstructLayers();
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

        /// <summary>
        /// Opens a screen of the provided type with the specified name on the specified layer
        /// </summary>
        public T OpenScreen<T>(string ScreenName, MDScreenLayer ScreenLayer) where T : MDScreen
        {
            return OpenScreen(typeof(T), ScreenName, ScreenLayer) as T;
        }

        /// <summary>
        /// Opens a screen of the provided type with the specified name on the specified layer
        /// </summary>
        public MDScreen OpenScreen(Type ScreenType, string ScreenName, MDScreenLayer ScreenLayer)
        {
            MDScreen NewScreen = MDStatics.CreateTypeInstance<MDScreen>(ScreenType);
            AddScreenToStack(NewScreen, ScreenLayer);

            return NewScreen;
        }

        /// <summary>
        /// Opens a screen of the provided type with the specified name on the specified layer
        /// </summary>
        public MDScreen OpenScreen(PackedScene ScreenScene, string ScreenName, MDScreenLayer ScreenLayer)
        {
            MDScreen NewScreen = ScreenScene.Instance() as MDScreen;
            if (NewScreen != null)
            {
                NewScreen.Name = ScreenName;
                AddScreenToStack(NewScreen, ScreenLayer);
            }

            return NewScreen;
        }

        private void AddScreenToStack(MDScreen Screen, MDScreenLayer ScreenLayer)
        {
            MDLayerStack LayerStack = GetLayerStack(ScreenLayer);
            LayerStack.AddScreen(Screen);
        }

        private MDLayerStack GetLayerStack(MDScreenLayer ScreenLayer)
        {
            return LayerMap[ScreenLayer];
        }

        private void ConstructLayers()
        {
            foreach (MDScreenLayer ScreenLayer in (MDScreenLayer[]) Enum.GetValues(typeof(MDScreenLayer)))
            {
                MDLayerStack LayerStack = new MDLayerStack
                {
                    Name = ScreenLayer.ToString() + "Layer",
                    LayerType = ScreenLayer
                };

                LayerMap.Add(ScreenLayer, LayerStack);
                AddChild(LayerStack);
            }
        }

        // Opens and focuses the console UI
        private void ToggleConsole()
        {
            if (Console == null)
            {
                Console = OpenScreen<MDConsole>(ConsoleName, MDScreenLayer.Console);
                Console.OnScreenClosed += OnConsoleClosed;
            }
            else
            {
                Console.CloseScreen();
            }
        }

        private void OnConsoleClosed(MDScreen Screen)
        {
            if (Screen == Console)
            {
                Console.OnScreenClosed -= OnConsoleClosed;
                Console = null;
            }
        }

        private void ToggleOnScreenDebug()
        {
            if (OnScreenDebug == null)
            {
                OnScreenDebug = OpenScreen<MDOnScreenDebug>(OnScreenDebugName, MDScreenLayer.Debug);
                OnScreenDebug.OnScreenClosed += OnOnScreenDebugClosed;
            }
            else
            {
                OnScreenDebug.CloseScreen();
            }
        }

        private void OnOnScreenDebugClosed(MDScreen Screen)
        {
            if (OnScreenDebug == Screen)
            {
                OnScreenDebug.OnScreenClosed -= OnOnScreenDebugClosed;
                OnScreenDebug = null;
            }
        }
    }
}