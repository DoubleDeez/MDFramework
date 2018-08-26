using Godot;
using System;

/*
 * MDGameInstance
 *
 * Single-instance class that persists throughout the life-time of the game application.
 */
public class MDGameInstance : Node
{
    public override void _Ready()
    {
        MDArguments.PopulateArgs();
    }
}
