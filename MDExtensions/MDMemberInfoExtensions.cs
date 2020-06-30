using Godot;
using System;
using System.Reflection;

public static class MDMemberInfoExtensions
{
    public static void SetValue(this MemberInfo member, object Instance, object Value)
    {
        switch (member.MemberType)
        {
            case MemberTypes.Field:
                ((FieldInfo)member).SetValue(Instance, Value);
                break;
            case MemberTypes.Property:
                ((PropertyInfo)member).SetValue(Instance, Value);
                break;
            default:
                throw new ArgumentException
                (
                "Input MemberInfo must be if type FieldInfo or PropertyInfo"
                );
        }
    }

    public static Type GetUnderlyingType(this MemberInfo member)
    {
        switch (member.MemberType)
        {
            case MemberTypes.Event:
                return ((EventInfo)member).EventHandlerType;
            case MemberTypes.Field:
                return ((FieldInfo)member).FieldType;
            case MemberTypes.Method:
                return ((MethodInfo)member).ReturnType;
            case MemberTypes.Property:
                return ((PropertyInfo)member).PropertyType;
            default:
                throw new ArgumentException
                (
                "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                );
        }
    }
}
