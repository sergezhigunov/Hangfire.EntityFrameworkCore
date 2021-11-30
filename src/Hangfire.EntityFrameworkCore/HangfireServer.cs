using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireServer
{
    [MaxLength(256)]
    public string Id { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime Heartbeat { get; set; }

    public int WorkerCount { get; set; }

    [Required]
    public string Queues { get; set; }
}
