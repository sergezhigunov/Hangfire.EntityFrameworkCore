using System.Diagnostics.CodeAnalysis;
using Hangfire.EntityFrameworkCore.Properties;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Stores options that configure the operation of methods on the
/// <see cref="EFCoreStorage"/> class.
/// </summary>
public class EFCoreStorageOptions
{
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
        get;
        set
        {
            ThrowIfNonPositive(value);
            field = value;
        }
    } = new(0, 10, 0);

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
        get;
        set
        {
            ThrowIfNonPositive(value);
            field = value;
        }
    } = new(0, 0, 15);

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
        get;
        set
        {
            ThrowIfNonPositive(value);
            field = value;
        }
    } = new(0, 5, 0);

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
        get;
        set
        {
            ThrowIfNonPositive(value);
            field = value;
        }
    } = new(0, 30, 0);

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
        get;
        set
        {
            ThrowIfNonPositive(value);
            field = value;
        }
    } = new(0, 5, 0);

    /// <summary>
    /// Apply a sliding invisibility timeout where the last fetched time is continually updated in the background.
    /// This allows a lower invisibility timeout to be used with longer running jobs
    /// IMPORTANT: If <see cref="BackgroundJobServerOptions.IsLightweightServer" /> option is used, then sliding
    /// invisibility timeouts will not work since the background storage processes are not run
    /// (which is used to update the invisibility timeouts)
    /// </summary>
    public bool UseSlidingInvisibilityTimeout { get; set; }

    /// <summary>
    /// Gets or set DB storage schema name. The <see cref="string.Empty"/> value means that the provider-specific
    /// default schema name will be used. NOT applicable if uses external <see cref="DbContext"/> type.
    /// The default value is <see cref="string.Empty"/>.
    /// </summary>
    /// <value>
    /// A schema name.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    [SuppressMessage("Maintainability", "CA1510")]
    public string Schema
    {
        get;
        set
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(value);
#else
            if (value is null) throw new ArgumentNullException(nameof(value));
#endif
            field = value;
        }
    } = string.Empty;

    private static void ThrowIfNonPositive(TimeSpan value)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
#else
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                CoreStrings.ArgumentOutOfRangeExceptionNeedPositiveValue);
#endif
    }
}
