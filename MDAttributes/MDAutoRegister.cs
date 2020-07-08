using System;

namespace MD
{
    public enum MDAutoRegisterType
    {
        None,
        Production, // Only register production features, such as replication and MDBindNode
        Debug // Includes debug features, such as commands
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class MDAutoRegister : Attribute
    {
        public MDAutoRegisterType RegisterType { private set; get; }
        public MDAutoRegister(MDAutoRegisterType InRegisterType = MDAutoRegisterType.Production)
        {
            RegisterType = InRegisterType;
        }

    }
}