using System;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Stores options that configure the operation of methods on the
    /// <see cref="EFCoreStorage"/> class.
    /// </summary>
    public class EFCoreStorageOptions
    {
        private TimeSpan _distributedLockTimeout = new TimeSpan(0, 10, 0);
        private TimeSpan _queuePollInterval = new TimeSpan(0, 0, 15);
        private TimeSpan _countersAggregationInterval = new TimeSpan(0, 5, 0);
        private TimeSpan _jobExpirationCheckInterval = new TimeSpan(0, 30, 0);
        private TimeSpan _slidingInvisibilityTimeout = new TimeSpan(0, 5, 0);
        private string _defaultSchemaName = "Hangfire";

        /// <summary>
        /// Gets or set maximal distributed lock lifetime. The default value is 00:10:00.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> value.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less or equal to <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public TimeSpan DistributedLockTimeout
        {
            get => _distributedLockTimeout;
            set
            {
                ThrowIfNonPositive(value);
                _distributedLockTimeout = value;
            }
        }

        /// <summary>
        /// Gets or set queue polling interval. The default value is 00:00:15.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> value.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less or equal to <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public TimeSpan QueuePollInterval
        {
            get => _queuePollInterval;
            set
            {
                ThrowIfNonPositive(value);
                _queuePollInterval = value;
            }
        }

        /// <summary>
        /// Gets or set interval between counter aggregation executions.
        /// The default value is 00:05:00.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> value.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less or equal to <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public TimeSpan CountersAggregationInterval
        {
            get => _countersAggregationInterval;
            set
            {
                ThrowIfNonPositive(value);
                _countersAggregationInterval = value;
            }
        }

        /// <summary>
        /// Gets or set interval between expiration manager executions.
        /// The default value is 00:30:00.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> value.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less or equal to <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public TimeSpan JobExpirationCheckInterval
        {
            get => _jobExpirationCheckInterval;
            set
            {
                ThrowIfNonPositive(value);
                _jobExpirationCheckInterval = value;
            }
        }

        /// <summary>
        /// Gets or set fetched job invisibility timeout. The default value is 00:05:00.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> value.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="value"/> is less or equal to <see cref="TimeSpan.Zero"/>.
        /// </exception>
        public TimeSpan SlidingInvisibilityTimeout
        {
            get => _slidingInvisibilityTimeout;
            set
            {
                ThrowIfNonPositive(value);
                _slidingInvisibilityTimeout = value;
            }
        }

        /// <summary>
        /// Gets or set DB storage schema name. The default value is "Hangfire".
        /// </summary>
        /// <value>
        /// A schema name.
        /// </value>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        public string DefaultSchemaName
        {
            get => _defaultSchemaName;
            set => _defaultSchemaName = value ?? throw new ArgumentNullException(nameof(value));
        }

        private static void ThrowIfNonPositive(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }
    }
}
