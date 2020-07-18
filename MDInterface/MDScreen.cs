using Godot;
using System;

namespace MD
{
    /// <summary>
    /// The base class for any screen to be used with the Interface Manager, will be displayed full screen
    /// </summary>
    public class MDScreen : Control
    {
        /// <summary>
        /// Generic Event Handler for Events from <c>MDScreen</c>
        /// </summary>
        /// <param name="Screen">The screen the event is triggered from</param>
        public delegate void ScreenEventHandler(MDScreen Screen);
    
        /// <summary>
        /// Triggered right before the scene is removed from the Interface Manager
        /// </summary>
        public event ScreenEventHandler OnScreenClosed = delegate { };

        public override void _Ready()
        {
            base._Ready();

            this.SetAnchor(0, 0, 1, 1);
            this.SetMargin(0, 0, 0, 0);
        }

        /// <summary>
        /// Removes this screen from the screen stack
        /// </summary>
        /// <param name="Free">Whether or not the Node should be freed (default: true)</param>
        public void CloseScreen(bool Free = true)
        {
            OnScreenClosed(this);

            if (Free)
            {
                this.RemoveAndFree();
            }
            else
            {
                this.RemoveFromParent();
            }
        }
    }
}