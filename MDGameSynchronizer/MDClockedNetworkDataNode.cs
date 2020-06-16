using Godot;
using System;

public class MDClockedNetworkDataNode : Node
{
    /*
        MDClockedNetworkData(float interval) // Interval is how often this property is sent over the network in seconds    
        
        AddProperty(int id, MDClockedNetworkProperty property)
        GetProperty(int id)         // Returns the MDClockedNetworkProperty
        T GetPropertyValue<T>(int id)     // Returns the value of the property at this tick
        
        // Will handle sending and recieve internally
    */

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        
    }
}
