namespace Hangfire.EntityFrameworkCore;

internal static class TimestampHelper
{
    public static long GetTimestamp()
    {
#if NET8_0_OR_GREATER
        return Environment.TickCount64;
#else
        return Environment.TickCount;
#endif
    }

    public static TimeSpan Elapsed(long timestamp)
    {
        long now = GetTimestamp();
        return Elapsed(now, timestamp);
    }

    public static TimeSpan Elapsed(long now, long timestamp)
    {
#if NET8_0_OR_GREATER
        return TimeSpan.FromMilliseconds(now - timestamp);
#else
        return TimeSpan.FromMilliseconds(unchecked((int)now - (int)timestamp));
#endif
    }
}
