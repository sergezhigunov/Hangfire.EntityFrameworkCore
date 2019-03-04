using System;
using Xunit;

namespace Hangfire.EntityFrameworkCore.Tests
{
    public static class EFCoreStorageOptionsFacts
    {
        [Fact]
        public static void Ctor_CreatesInstance()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Equal(new TimeSpan(0, 10, 0), Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_distributedLockTimeout")));
            Assert.Equal(new TimeSpan(0, 0, 15), Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_queuePollInterval")));
            Assert.Equal(new TimeSpan(0, 5, 0), Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_countersAggregationInterval")));
            Assert.Equal(new TimeSpan(0, 30, 0), Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_jobExpirationCheckInterval")));
            Assert.Equal(new TimeSpan(0, 5, 0), Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_slidingInvisibilityTimeout")));
            Assert.Empty(Assert.IsType<string>(instance.GetFieldValue("_schema")));
        }

        [Fact]
        public static void DistributedLockTimeout_Throws_WhenValueIsNonPositive()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.DistributedLockTimeout = default);

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.DistributedLockTimeout = new TimeSpan(-1));
        }

        [Fact]
        public static void DistributedLockTimeout_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = new TimeSpan(0, 20, 0);

            instance.DistributedLockTimeout = value;

            Assert.Equal(value, instance.DistributedLockTimeout);
            Assert.Equal(value, Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_distributedLockTimeout")));
        }

        [Fact]
        public static void QueuePollInterval_Throws_WhenValueIsNonPositive()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.QueuePollInterval = default);

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.QueuePollInterval = new TimeSpan(-1));
        }

        [Fact]
        public static void QueuePollInterval_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = new TimeSpan(0, 20, 0);

            instance.QueuePollInterval = value;

            Assert.Equal(value, instance.QueuePollInterval);
            Assert.Equal(value, Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_queuePollInterval")));
        }

        [Fact]
        public static void CountersAggregationInterval_Throws_WhenValueIsNonPositive()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.CountersAggregationInterval = default);

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.CountersAggregationInterval = new TimeSpan(-1));
        }

        [Fact]
        public static void CountersAggregationInterval_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = new TimeSpan(0, 20, 0);

            instance.CountersAggregationInterval = value;

            Assert.Equal(value, instance.CountersAggregationInterval);
            Assert.Equal(value, Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_countersAggregationInterval")));
        }

        [Fact]
        public static void JobExpirationCheckInterval_Throws_WhenValueIsNonPositive()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.JobExpirationCheckInterval = default);

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.JobExpirationCheckInterval = new TimeSpan(-1));
        }

        [Fact]
        public static void JobExpirationCheckInterval_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = new TimeSpan(0, 20, 0);

            instance.JobExpirationCheckInterval = value;

            Assert.Equal(value, instance.JobExpirationCheckInterval);
            Assert.Equal(value, Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_jobExpirationCheckInterval")));
        }

        [Fact]
        public static void SlidingInvisibilityTimeout_Throws_WhenValueIsNonPositive()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.SlidingInvisibilityTimeout = default);

            Assert.Throws<ArgumentOutOfRangeException>("value",
                () => instance.SlidingInvisibilityTimeout = new TimeSpan(-1));
        }

        [Fact]
        public static void SlidingInvisibilityTimeout_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = new TimeSpan(0, 20, 0);

            instance.SlidingInvisibilityTimeout = value;

            Assert.Equal(value, instance.SlidingInvisibilityTimeout);
            Assert.Equal(value, Assert.IsType<TimeSpan>(
                instance.GetFieldValue("_slidingInvisibilityTimeout")));
        }

        [Fact]
        public static void Schema_Throws_WhenValueIsNull()
        {
            var instance = new EFCoreStorageOptions();

            Assert.Throws<ArgumentNullException>("value",
                () => instance.Schema = default);
        }

        [Fact]
        public static void Schema_GetsAndSetsCorrectly()
        {
            var instance = new EFCoreStorageOptions();
            var value = "test";

            instance.Schema = value;

            Assert.Equal(value, instance.Schema);
            Assert.Equal(value, Assert.IsType<string>(
                instance.GetFieldValue("_schema")));
        }
    }
}
