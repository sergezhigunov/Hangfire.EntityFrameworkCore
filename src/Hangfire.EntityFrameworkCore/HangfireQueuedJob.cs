using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireQueuedJob
{
    public long Id { get; set; }

    public long JobId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Queue { get; set; }

    [ConcurrencyCheck]
    public DateTime? FetchedAt { get; set; }

    public virtual HangfireJob Job { get; set; }
}
