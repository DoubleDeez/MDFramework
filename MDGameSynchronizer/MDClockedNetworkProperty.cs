using Godot;
using System;

public class MDClockedNetworkProperty<T>
{
    /*
        ClockedPropertyModes 
        {  
            INTERPOLATED,   // Works only for vector2 (Could do others in the future)
            ONE_SHOT,       // Will return the given value on the given tick
            ON_CHANGE       // Will return the last known property
        }
        
        ClockedPropertySendModes
        {
            GROUPED,     // Sent along with rest of grouped properties
            ON_SET    // Sends as soon as property is set
        }
        
        MDClockedNetworkProperty(T DefaultValue, ClockedPropertyMode Mode, T PropertyMemberRef, int BufferSize = 30)  // Buffer size is how many properties to keep in memory
        
        T GetPropertyValue()            // Gets property for current tick
        T GetPropertyValue(uint Tick)   // Get the property value for the given tick if we have it or can calculate it
        
        event OnPropertyChanged(T value)     // Sent whenever property is changed
    */
}
