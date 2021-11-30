using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore;

internal class HangfireJob : IExpirable
{
    public HangfireJob()
    {
        Parameters = new HashSet<HangfireJobParameter>();
        QueuedJobs = new HashSet<HangfireQueuedJob>();
        States = new HashSet<HangfireState>();
    }

    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? StateId { get; set; }

    [MaxLength(256)]
    public string StateName { get; set; }

    public DateTime? ExpireAt { get; set; }

    [Required]
    public string InvocationData { get; set; }

    public virtual HangfireState State { get; set; }

    public virtual ICollection<HangfireJobParameter> Parameters { get; set; }

    public virtual ICollection<HangfireQueuedJob> QueuedJobs { get; set; }

    public virtual ICollection<HangfireState> States { get; set; }
}
