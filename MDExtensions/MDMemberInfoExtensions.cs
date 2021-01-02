using System;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Extension class to provide useful framework methods
    /// </summary>
    public static class MDMemberInfoExtensions
    {
        public const string LOG_CAT = "LogMemberInfoExtension";

        /// <summary>
        /// Sets the value of this member
        /// </summary>
        /// <param name="member">The member</param>
        /// <param name="Instance">The instance to set the value for</param>
        /// <param name="Value">The value</param>
        public static void SetValue(this MemberInfo member, object Instance, object Value)
        {
            MDLog.Trace(LOG_CAT, $"Setting {member.Name}");
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

        /// <summary>
        /// Get the value of this member
        /// </summary>
        /// <param name="member">The member</param>
        /// <param name="Instance">The instance to get the value of</param>
        /// <returns>The value of the member in the instance</returns>
        public static object GetValue(this MemberInfo member, object Instance)
        {
            if (Instance == null)
            {
                return null;
            }
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

        /// <summary>
        /// Gets the underlying type of a member
        /// </summary>
        /// <param name="member">The member to find the type for</param>
        /// <returns>The underlying type</returns>
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