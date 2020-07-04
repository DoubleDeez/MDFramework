using Godot;
using System;

namespace MD
{
    public enum MDInputType
    {
        MouseAndKeyboard,
        JoyPad,
        Touch
    }

// Helper class that holds input state and delegates
    public class MDInput
    {
        private const string LOG_CAT = "MDInput";

        public delegate void InputChangeHandler(MDInputType OldInputType, MDInputType NewInputType);

        public event InputChangeHandler OnInputTypeChanged = delegate { };

        public MDInputType LastInputType { get; private set; } = MDInputType.MouseAndKeyboard;

        public void OnInputEvent(InputEvent Event)
        {
            MDInputType OldInputType = LastInputType;
            switch (Event)
            {
                case InputEventKey _:
                case InputEventMouse _:
                    LastInputType = MDInputType.MouseAndKeyboard;
                    break;
                case InputEventJoypadButton _:
                case InputEventJoypadMotion _:
                    LastInputType = MDInputType.JoyPad;
                    break;
                case InputEventScreenTouch _:
                case InputEventGesture _:
                    LastInputType = MDInputType.Touch;
                    break;
                default:
                    MDLog.Warn(LOG_CAT, "Unknown Input Event Type: {0}", Event.AsText());
                    break;
            }

            if (OldInputType != LastInputType)
            {
                OnInputTypeChanged(OldInputType, LastInputType);
            }
        }
    }
}