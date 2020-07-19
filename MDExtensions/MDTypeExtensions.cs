using System;
using System.Reflection;

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
    }
}