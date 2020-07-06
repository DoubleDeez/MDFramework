using System;

namespace MD
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class MDReplicatedSetting : Attribute
    {
        public MDReplicatedSetting(object InKey, object InValue)
        {
            this.Key = InKey;
            this.Value = InValue;
        }

        public object Key { private set; get; }
        public object Value { private set; get; }
    }
}