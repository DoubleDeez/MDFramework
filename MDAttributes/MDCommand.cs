using System;

[AttributeUsage(AttributeTargets.Method)]
public class MDCommand : Attribute
{
    public string HelpText {get; set;}
}