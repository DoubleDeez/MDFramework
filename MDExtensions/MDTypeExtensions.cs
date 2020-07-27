using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// Extension class to provide useful framework methods
    /// </summary>
    public static class MDTypeExtensions
    {
        public static MethodInfo[] GetAllMethods(this Type Instance)
        {
             return Instance.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
        }

        public static MethodInfo GetMethodRecursive(this Type Instance, string MethodName)
        {
            MethodInfo Result = null;
            Type CurType = Instance;
            while (CurType != null && Result == null)
            {
                Result = CurType.GetMethod(MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                CurType = CurType.BaseType;
            }

            return Result;
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
    }
}