using Godot;
using System;

public class CustomGameInstance : MDGameInstance
{
    public override bool UseUPNP()
    {
        //TODO: Remove this once configuration files are introduced
        return false;
    }
}
