using System;

namespace MD
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MDCommand : Attribute
    {
        public string HelpText { get; set; }

        public object[] DefaultArgs { get; set; }
    }
}