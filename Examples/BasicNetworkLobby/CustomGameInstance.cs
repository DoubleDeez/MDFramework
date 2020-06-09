using Godot;
using System;

public class CustomGameInstance : MDGameInstance
{
    public override bool UseUPNP()
    {
        return false;
    }
}
