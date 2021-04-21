using System;
using Hangfire.EntityFrameworkCore.Properties;

namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Stores options that configure the operation of methods on the
    /// <see cref="EFCoreStorage"/> class.
    /// </summary>
    public class EFCoreStorageOptions
    {
        private TimeSpan _distributedLockTimeout = new(0, 10, 0);
        private TimeSpan _queuePollInterval = new(0, 0, 15);
        private TimeSpan _countersAggregationInterval = new(0, 5, 0);
        private TimeSpan _jobExpirationCheckInterval = new(0, 30, 0);
        private TimeSpan _slidingInvisibilityTimeout = new(0, 5, 0);
        private string _schema = string.Empty;

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
        /// Gets or set DB storage schema name. The <see cref="string.Empty"/> value means that
        /// the provider-specific default schema name will be used.
        /// The default value is <see cref="string.Empty"/>.
        /// </summary>
        /// <value>
        /// A schema name.
        /// </value>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </exception>
        public string Schema
        {
            get => _schema;
            set
            {
                if (value is null)
                    throw new ArgumentNullException(nameof(value));
                _schema = value;
            }
        }

        private static void ThrowIfNonPositive(TimeSpan value)
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    CoreStrings.ArgumentOutOfRangeExceptionNeedPositiveValue);
        }
    }
}
