using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a job parameter.
/// </summary>
public class HangfireJobParameter
{
    /// <summary>
    /// Gets or sets the owner job ID.
    /// </summary>
    public long JobId { get; set; }

    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    [MaxLength(256)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the parameter value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the owner job.
    /// </summary>
    public virtual HangfireJob Job { get; set; }
}
