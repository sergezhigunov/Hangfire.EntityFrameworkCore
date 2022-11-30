namespace Hangfire.EntityFrameworkCore;

internal static class DictionaryExtensions
{
    internal static TValue GetValue<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key)
    {
        if (dictionary.TryGetValue(key, out var value))
            return value;
        return default;
    }
}
