using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a job state.
/// </summary>
public class HangfireState
{
    /// <summary>
    /// Gets or sets the primary key for this state.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the owner job ID.
    /// </summary>
    public long JobId { get; set; }

    /// <summary>
    /// Gets or sets the state name.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the reason caused this state.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the job state creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the job state data.
    /// </summary>
    [Required]
    public IDictionary<string, string> Data { get; set; }

    /// <summary>
    /// Gets or sets the owner job.
    /// </summary>
    public virtual HangfireJob Job { get; set; }
}
