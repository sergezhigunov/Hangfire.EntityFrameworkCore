using System;
using System.Reflection;

namespace Hangfire.EntityFrameworkCore.Tests
{
    internal static class ReflectionExtensions
    {
        public static T CreateInstance<T>(params object[] args)
        {
            return (T)Activator.CreateInstance(typeof(T),
                BindingFlags.NonPublic | BindingFlags.Instance, null, args, null);
        }

        public static object GetFieldValue(this object instance, string name)
        {
            return instance.GetType().
                GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }
    }
}
