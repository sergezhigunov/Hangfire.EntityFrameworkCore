using System.Diagnostics.CodeAnalysis;
using NotNullAttribute = Hangfire.Annotations.NotNullAttribute;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a first-in, first-out collection of Hangfire jobs.
/// </summary>
[SuppressMessage("Naming", "CA1711")]
public interface IPersistentJobQueue
{
    /// <summary>
    /// Retrieves the job at the head of the queue and returns it.
    /// If the queue is empty, this method waits for the job to appear in the queue.
    /// </summary>
    /// <param name="queues">
    /// A collection of the queues from which the job is being retrieved.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel this operation.
    /// </param>
    /// <returns>
    /// A job that can be returned to the queue or permanently removed from it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="queues"/> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="queues"/> is empty.
    /// </exception>
    [NotNull]
    IFetchedJob Dequeue([NotNull] string[] queues, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a job to the tail of the queue.
    /// </summary>
    /// <param name="queue">
    /// The queue to which the job is being added.
    /// </param>
    /// <param name="jobId">
    /// The job identifier.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="queue"/> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="jobId"/> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="jobId"/> is not exists.
    /// </exception>
    void Enqueue([NotNull] string queue, [NotNull] string jobId);
}
