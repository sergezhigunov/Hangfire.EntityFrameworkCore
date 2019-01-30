using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireServer
    {
        [MaxLength(100)]
        public string Id { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime Heartbeat { get; set; }

        public int WorkerCount { get; set; }

        [Required]
        public IList<string> Queues { get; set; }
    }
}
