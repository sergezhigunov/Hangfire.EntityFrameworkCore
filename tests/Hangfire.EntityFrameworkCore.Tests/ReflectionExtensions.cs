using System.Reflection;

namespace Hangfire.EntityFrameworkCore.Tests
{
    internal static class ReflectionExtensions
    {
        public static object GetFieldValue(this object instance, string name)
        {
            return instance.GetType().
                GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).
                GetValue(instance);
        }
    }
}
