namespace Hangfire.EntityFrameworkCore.Tests;

public static class EFCoreStorageOptionsFacts
{
    [Fact]
    public static void Ctor_CreatesInstance()
    {
        var instance = new EFCoreStorageOptions();

        Assert.Equal(new TimeSpan(0, 10, 0), instance.DistributedLockTimeout);
        Assert.Equal(new TimeSpan(0, 0, 15), instance.QueuePollInterval);
        Assert.Equal(new TimeSpan(0, 5, 0), instance.CountersAggregationInterval);
        Assert.Equal(new TimeSpan(0, 30, 0), instance.JobExpirationCheckInterval);
        Assert.Equal(new TimeSpan(0, 5, 0), instance.SlidingInvisibilityTimeout);
        Assert.Empty(instance.Schema);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void DistributedLockTimeout_Throws_WhenValueIsNonPositive(long ticks)
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(ticks);

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value),
            () => instance.DistributedLockTimeout = value);
    }

    [Fact]
    public static void DistributedLockTimeout_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(0, 20, 0);

        instance.DistributedLockTimeout = value;

        Assert.Equal(value, instance.DistributedLockTimeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void QueuePollInterval_Throws_WhenValueIsNonPositive(long ticks)
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(ticks);

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value),
            () => instance.QueuePollInterval = value);
    }

    [Fact]
    public static void QueuePollInterval_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(0, 20, 0);

        instance.QueuePollInterval = value;

        Assert.Equal(value, instance.QueuePollInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void CountersAggregationInterval_Throws_WhenValueIsNonPositive(long ticks)
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(ticks);

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value),
            () => instance.CountersAggregationInterval = value);
    }

    [Fact]
    public static void CountersAggregationInterval_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(0, 20, 0);

        instance.CountersAggregationInterval = value;

        Assert.Equal(value, instance.CountersAggregationInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void JobExpirationCheckInterval_Throws_WhenValueIsNonPositive(long ticks)
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(ticks);

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value),
            () => instance.JobExpirationCheckInterval = value);
    }

    [Fact]
    public static void JobExpirationCheckInterval_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(0, 20, 0);

        instance.JobExpirationCheckInterval = value;

        Assert.Equal(value, instance.JobExpirationCheckInterval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public static void SlidingInvisibilityTimeout_Throws_WhenValueIsNonPositive(long ticks)
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(ticks);

        Assert.Throws<ArgumentOutOfRangeException>(nameof(value),
            () => instance.SlidingInvisibilityTimeout = value);

     }

    [Fact]
    public static void SlidingInvisibilityTimeout_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = new TimeSpan(0, 20, 0);

        instance.SlidingInvisibilityTimeout = value;

        Assert.Equal(value, instance.SlidingInvisibilityTimeout);
    }

    [Fact]
    public static void Schema_Throws_WhenValueIsNull()
    {
        var instance = new EFCoreStorageOptions();
        var value = default(string)!;

        Assert.Throws<ArgumentNullException>(nameof(value),
            () => instance.Schema = default);
    }

    [Fact]
    public static void Schema_GetsAndSetsCorrectly()
    {
        var instance = new EFCoreStorageOptions();
        var value = "test";

        instance.Schema = value;

        Assert.Equal(value, instance.Schema);
    }
}
