using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a Hangfire job placed in a queue.
/// </summary>
public class HangfireQueuedJob
{
    /// <summary>
    /// Gets or sets the primary key for this place.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owner job ID.
    /// </summary>
    public long JobId { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Queue { get; set; }

    /// <summary>
    /// Gets or sets last fetch timestamp.
    /// </summary>
    [ConcurrencyCheck]
    public DateTime? FetchedAt { get; set; }

    /// <summary>
    /// Gets or sets the owner job.
    /// </summary>
    public virtual HangfireJob Job { get; set; }
}
