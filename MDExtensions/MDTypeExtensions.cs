using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Godot;

namespace MD
{
    /// <summary>
    /// Extension class to provide useful framework methods
    /// </summary>
    public static class MDTypeExtensions
    {
        public static MethodInfo GetMethodRecursive(this Type Instance, string MethodName, bool IgnoreCase)
        {
            MethodInfo Result = null;
            Type CurType = Instance;
            BindingFlags Flags = MDStatics.BindFlagsAll;
            if (IgnoreCase)
            {
                Flags = MDStatics.BindFlagsAllIgnoreCase;
            }
            while (CurType != null && Result == null)
            {
                Result = CurType.GetMethod(MethodName, Flags);
                CurType = CurType.BaseType;
            }

            return Result;
        }

        public static MethodInfo GetMethodRecursive(this Type Instance, string MethodName)
        {
            return GetMethodRecursive(Instance, MethodName, false);
        }

        public static MethodInfo GetMethodRecursive(this Type Instance, string MethodName, Type[] Types, bool IgnoreCase)
        {
            MethodInfo Result = null;
            Type CurType = Instance;
            BindingFlags Flags = MDStatics.BindFlagsAll;
            if (IgnoreCase)
            {
                Flags = MDStatics.BindFlagsAllIgnoreCase;
            }
            while (CurType != null && Result == null)
            {
                Result = CurType.GetMethod(MethodName, Flags, null, Types, null);
                CurType = CurType.BaseType;
            }

            return Result;
        }

        public static MethodInfo GetMethodRecursive(this Type Instance, string MethodName, Type[] Types)
        {
            return GetMethodRecursive(Instance, MethodName, Types, false);
        }

        public static MemberInfo GetMemberRecursive(this Type Instance, string MemberName, bool IgnoreCase)
        {
            MemberInfo Result = null;
            Type CurType = Instance;
            BindingFlags Flags = MDStatics.BindFlagsAll;
            if (IgnoreCase)
            {
                Flags = MDStatics.BindFlagsAllIgnoreCase;
            }
            while (CurType != null && Result == null)
            {
                Result = CurType.GetField(MemberName, Flags);
                if (Result == null)
                {
                    Result = CurType.GetProperty(MemberName, Flags);
                }

                CurType = CurType.BaseType;
            }

            return Result;
        }

        public static MemberInfo GetMemberRecursive(this Type Instance, string MemberName)
        {
            return GetMemberRecursive(Instance, MemberName, false);
        }

        public static bool IsCastableTo(this Type from, Type to, bool implicitly = false)
        {
            return to.IsAssignableFrom(from) || from.HasCastDefined(to, implicitly);
        }

        static bool HasCastDefined(this Type from, Type to, bool implicitly)
        {
            if ((from.IsPrimitive || from.IsEnum) && (to.IsPrimitive || to.IsEnum))
            {
                if (!implicitly)
                {
                    return from==to || (from!=typeof(Boolean) && to!=typeof(Boolean));
                }

                Type[][] typeHierarchy = {
                    new Type[] { typeof(Byte),  typeof(SByte), typeof(Char) },
                    new Type[] { typeof(Int16), typeof(UInt16) },
                    new Type[] { typeof(Int32), typeof(UInt32) },
                    new Type[] { typeof(Int64), typeof(UInt64) },
                    new Type[] { typeof(Single) },
                    new Type[] { typeof(Double) }
                };

                IEnumerable<Type> lowerTypes = Enumerable.Empty<Type>();
                foreach (Type[] types in typeHierarchy)
                {
                    if ( types.Any(t => t == to) )
                    {
                        return lowerTypes.Any(t => t == from);
                    }

                    lowerTypes = lowerTypes.Concat(types);
                }

                return false;   // IntPtr, UIntPtr, Enum, Boolean
            }

            return IsCastDefined(to, m => m.GetParameters()[0].ParameterType, _ => from, implicitly, false)
                || IsCastDefined(from, _ => to, m => m.ReturnType, implicitly, true);
        }

        static bool IsCastDefined(Type type, Func<MethodInfo, Type> baseType,
                                Func<MethodInfo, Type> derivedType, bool implicitly, bool lookInBase)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.Static | (lookInBase ? BindingFlags.FlattenHierarchy : BindingFlags.DeclaredOnly);
            return type.GetMethods(bindingFlags).Any(
                m => (m.Name=="op_Implicit" || (!implicitly && m.Name=="op_Explicit"))
                    && baseType(m).IsAssignableFrom(derivedType(m)));
        }

        public static bool IsNullable(this Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// Returns a list of all the unique members for a Node, including the hierarchy
        /// </summary>
        /// <param name="Instance">The object type to find for</param>
        /// <returns>List of members</returns>
        public static IList<MemberInfo> GetMemberInfos(this Type Instance)
        {
            return MDReflectionCache.GetMemberInfos(Instance);
        }

        /// <summary>
        /// Get all members with the given attribute inside the type
        /// </summary>
        /// <param name="Type">The type to search inside</param>
        /// <typeparam name="T">The attribute to search for</typeparam>
        /// <returns>List of MemberInfos that has the attribute or an empty list</returns>
        public static List<MemberInfo> GetAllMembersWithAttribute<T>(this Type Type) where T : Attribute
        {
            IList<MemberInfo> Members = Type.GetMemberInfos();
            List<MemberInfo> ReturnList = new List<MemberInfo>();
            foreach (MemberInfo Member in Members)
            {
                T RepAttribute = MDReflectionCache.GetCustomAttribute<T>(Member) as T;
                if (RepAttribute == null)
                {
                    continue;
                }

                ReturnList.Add(Member);
            }

            return ReturnList;
        }

        /// <summary>
        /// Find all methods with the given attribute inside the type
        /// </summary>
        /// <param name="Type">The type to search inside</param>
        /// <typeparam name="T">The attribute to search for</typeparam>
        /// <returns>List of MethodInfos that has the attribute or an empty list</returns>
        public static List<MethodInfo> GetAllMethodsWithAttribute<T>(this Type Type) where T : Attribute
        {
            IList<MethodInfo> Methods = Type.GetMethodInfos();
            List<MethodInfo> ReturnList = new List<MethodInfo>();
            foreach (MethodInfo Method in Methods)
            {
                T RepAttribute = MDReflectionCache.GetCustomAttribute<T>(Method) as T;;
                if (RepAttribute == null)
                {
                    continue;
                }

                ReturnList.Add(Method);
            }

            return ReturnList;
        }

        /// <summary>
        /// Returns a list of all the unique methods for a Node, including the hierarchy
        /// </summary>
        /// <param name="Instance">The object type to find for</param>
        /// <returns>List of methodss</returns>
        public static IList<MethodInfo> GetMethodInfos(this Type Instance)
        {
            return MDReflectionCache.GetMethodInfos(Instance);
        }
    }
}