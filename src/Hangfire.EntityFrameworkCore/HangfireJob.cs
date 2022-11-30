using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a Hangfire job.
/// </summary>
public class HangfireJob : IExpirable
{
    /// <summary>
    /// Initializes a new instance of <see cref="HangfireJob"/>.
    /// </summary>
    public HangfireJob()
    {
        Parameters = new HashSet<HangfireJobParameter>();
        QueuedJobs = new HashSet<HangfireQueuedJob>();
        States = new HashSet<HangfireState>();
    }

    /// <summary>
    /// Gets or sets the primary key for this job.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the job creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the job current job state ID.
    /// </summary>
    public long? StateId { get; set; }

    /// <summary>
    /// Gets or sets the job current job name.
    /// </summary>
    /// <remarks>
    /// The property used for optimization.
    /// </remarks>
    [MaxLength(256)]
    public string StateName { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// Gets or sets the job invocation data.
    /// </summary>
    [Required]
    public InvocationData InvocationData { get; set; }

    /// <summary>
    /// Gets or sets the current job state.
    /// </summary>
    public virtual HangfireState State { get; set; }

    /// <summary>
    /// Gets or sets the job parameter collection.
    /// </summary>
    public virtual ICollection<HangfireJobParameter> Parameters { get; set; }

    /// <summary>
    /// Gets or sets the queue job executions.
    /// </summary>
    public virtual ICollection<HangfireQueuedJob> QueuedJobs { get; set; }

    /// <summary>
    /// Gets or sets the job state history.
    /// </summary>
    public virtual ICollection<HangfireState> States { get; set; }
}
