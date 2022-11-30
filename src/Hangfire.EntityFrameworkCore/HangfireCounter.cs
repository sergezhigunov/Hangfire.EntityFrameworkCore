using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a counter by key system in the Hangfire storage.
/// </summary>
public class HangfireCounter : IExpirable
{
    /// <summary>
    /// Gets or sets the primary key for this counter.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the name for this counter.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the value for this counter.
    /// </summary>
    public long Value { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpireAt { get; set; }
}
