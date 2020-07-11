using System;
using System.Reflection;

namespace MD
{
    public static class MDMemberInfoExtensions
    {
        public const string LOG_CAT = "LogMemberInfoExtension";

        public static void SetValue(this MemberInfo member, object Instance, object Value)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    ((FieldInfo) member).SetValue(Instance, Value);
                    break;
                case MemberTypes.Property:
                    ((PropertyInfo) member).SetValue(Instance, Value);
                    break;
                default:
                    MDLog.Error(LOG_CAT,
                        $"Input MemberInfo was of type {member.MemberType.ToString()}, it should be of type FieldInfo or PropertyInfo");
                    break;
            }
        }

        public static object GetValue(this MemberInfo member, object Instance)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo) member).GetValue(Instance);
                case MemberTypes.Property:
                    return ((PropertyInfo) member).GetValue(Instance);
                default:
                    MDLog.Error(LOG_CAT,
                        $"Input MemberInfo was of type {member.MemberType.ToString()}, it should be of type FieldInfo or PropertyInfo");
                    break;
            }
            return null;
        }

        public static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo) member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo) member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo) member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo) member).PropertyType;
                default:
                    MDLog.Error(LOG_CAT,
                        $"Input MemberInfo was of type {member.MemberType.ToString()}, it should be of type EventInfo, FieldInfo, MethodInfo, or PropertyInfo");
                    return null;
            }
        }
    }
}