using Godot;
using System.Collections.Generic;

namespace MD
{

    /// <summary>
    /// A stack of <c>MDScreen</c>, its <c>LayerType</c> determines how it functions
    /// </summary>
    public class MDLayerStack : Control
    {
        /// <summary>
        /// The screen layer type for this stack, determines how the layer functions.
        /// </summary>
        /// See <see cref="MD.MDScreenLayer"/> for details
        public MDScreenLayer LayerType { get; set; }

        private List<MDScreen> ScreenStack = new List<MDScreen>();
        
        public override void _Ready()
        {
            base._Ready();

            this.SetAnchor(0, 0, 1, 1);
            this.SetMargin(0, 0, 0, 0);
        }

        /// <summary>
        /// Add a screen to the top of the stack
        /// </summary>
        public void AddScreen(MDScreen Screen)
        {
            if (Screen != null)
            {
                // If it's in the stack, remove it, otherwise we need to bind its event
                if (ScreenStack.Remove(Screen) == false)
                {
                    Screen.OnScreenClosed += RemoveScreen;
                }

                ScreenStack.Add(Screen);
                AddChild(Screen);

                UpdateScreenVisibilities();
            }
        }

        /// <summary>
        /// Remove the provided screen from the stack
        /// </summary>
        public void RemoveScreen(MDScreen Screen)
        {
            if (Screen != null && ScreenStack.Contains(Screen))
            {
                Screen.OnScreenClosed -= RemoveScreen;

                UpdateScreenVisibilities();
            }
        }

        /// <summary>
        /// Remove the top screen from the stack
        /// </summary>
        public void PopScreen()
        {
            if (ScreenStack.Count > 0)
            {
                RemoveScreen(ScreenStack[ScreenStack.Count - 1]);
            }
        }

        private void UpdateScreenVisibilities()
        {
            // Only the Primary layer has special visiblility functionality
            if (LayerType == MDScreenLayer.Primary)
            {

            }
        }
    }
}