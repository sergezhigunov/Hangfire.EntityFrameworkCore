using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireServer
    {
        [MaxLength(100)]
        public string Id { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime StartedAt { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime Heartbeat { get; set; }

        public int WorkerCount { get; set; }

        [Required]
        public IReadOnlyList<string> Queues { get; set; }
    }
}
