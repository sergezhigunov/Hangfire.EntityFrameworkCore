namespace Hangfire.EntityFrameworkCore;

internal static class DictionaryExtensions
{
    extension<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
    {
        public TValue GetValue(TKey key)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;
            return default;
        }
    }
}
