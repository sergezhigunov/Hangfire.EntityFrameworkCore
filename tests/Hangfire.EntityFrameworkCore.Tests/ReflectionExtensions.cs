using System;
using System.Reflection;

namespace Hangfire.EntityFrameworkCore.Tests
{
    internal static class ReflectionExtensions
    {
        public static object GetFieldValue(this object instance, string name)
        {
            return GetField(instance.GetType(), name).
                GetValue(instance);
        }

        private static FieldInfo GetField(Type type, string name)
        {
            var result = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (result == null && type.BaseType != null)
                result = GetField(type.BaseType, name);
            return result;
        }
    }
}
