using Godot;
using System;

public enum MDInputType
{
    MouseAndKeyboard,
    Joypad,
    Touch
}

// Helper class that holds input state and delegates
public class MDInput
{
    
    public delegate void InputChangeHandler(MDInputType OldInputType, MDInputType NewInputType);
    public event InputChangeHandler OnInputTypeChanged = delegate {};

    public MDInputType LastInputType { get; private set;} = MDInputType.MouseAndKeyboard;

    private const string LOG_CAT = "MDInput";

    public void OnInputEvent(InputEvent Event)
    {
        MDInputType OldInputType = LastInputType;
        if ((Event is InputEventKey) || (Event is InputEventMouse))
        {
            LastInputType = MDInputType.MouseAndKeyboard;
        }
        else if ((Event is InputEventJoypadButton) || (Event is InputEventJoypadMotion))
        {
            LastInputType = MDInputType.Joypad;
        }
        else if ((Event is InputEventScreenTouch) || (Event is InputEventGesture))
        {
            LastInputType = MDInputType.Touch;
        }
        else
        {
            MDLog.Warn(LOG_CAT, "Unknown Input Event Type: {0}", Event.AsText());
        }

        if (OldInputType != LastInputType)
        {
            OnInputTypeChanged(OldInputType, LastInputType);
        }
    }
}
