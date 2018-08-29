using System;

[AttributeUsage(AttributeTargets.Method)]
public class MDCommandAttribute : Attribute
{
    public string HelpText {get; set;}
}