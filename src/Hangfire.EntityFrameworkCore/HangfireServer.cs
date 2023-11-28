using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Hangfire.EntityFrameworkCore;

/// <summary>
/// Represents an information about Hangfire working server.
/// </summary>
public class HangfireServer
{
    /// <summary>
    /// Gets or sets the server ID.
    /// </summary>
    [MaxLength(256)]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the server start timestamp.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the last heartbeat check timestamp.
    /// </summary>
    public DateTime Heartbeat { get; set; }

    /// <summary>
    /// Gets or sets the server worker threads processing jobs.
    /// </summary>
    public int WorkerCount { get; set; }

    /// <summary>
    /// Gets or sets the queue names list processing by the server.
    /// </summary>
    [Required]
    [SuppressMessage("Usage", "CA2227")]
    public IList<string> Queues { get; set; } = Array.Empty<string>();
}
