using System.Collections.Generic;
using Hangfire.Annotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Provides Hangfire job queue monitoring functionality.
/// </summary>
public interface IPersistentJobQueueMonitoringApi
{
    /// <summary>
    /// Returns an available queue collection.
    /// </summary>
    /// <returns>
    /// An available queue name collection.
    /// </returns>
    IList<string> GetQueues();

    /// <summary>
    /// Returns an enqueued job identifier collection in specified queue.
    /// </summary>
    /// <param name="queue">
    /// The queue name.
    /// </param>
    /// <param name="from">
    /// A specified number of elements to be bypassed in result.
    /// </param>
    /// <param name="perPage">
    /// A specified number of elements to be limited in result.
    /// </param>
    /// <returns>
    /// An enqueued job identifier collection.
    /// </returns>
    IList<string> GetEnqueuedJobIds([NotNull] string queue, int from, int perPage);

    /// <summary>
    /// Returns a fetched job identifier collection in specified queue.
    /// </summary>
    /// <param name="queue">
    /// The queue name.
    /// </param>
    /// <param name="from">
    /// A specified number of elements to be bypassed in result.
    /// </param>
    /// <param name="perPage">
    /// A specified number of elements to be limited in result.
    /// </param>
    /// <returns>
    /// A fetched job identifier collection.
    /// </returns>
    IList<string> GetFetchedJobIds([NotNull] string queue, int from, int perPage);

    /// <summary>
    /// Returns a specified queue statistics.
    /// </summary>
    /// <param name="queue">
    /// The queue name.
    /// </param>
    /// <returns>
    /// A queue statistics containing counts of enqueued and fetched jobs.
    /// </returns>
    QueueStatisticsDto GetQueueStatistics([NotNull] string queue);
}
