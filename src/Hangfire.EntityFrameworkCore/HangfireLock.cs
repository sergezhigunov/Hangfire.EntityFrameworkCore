using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents a named distributed lock.
/// </summary>
public class HangfireLock
{
    /// <summary>
    /// Gets or sets the lock name.
    /// </summary>
    [StringLength(256)]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the lock acquiring timestamp.
    /// </summary>
    public DateTime AcquiredAt { get; set; }
}
