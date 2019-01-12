using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireJobQueue
    {
        public long Id { get; set; }

        public long JobId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Queue { get; set; }

        public DateTime FetchedAt { get; set; }

        public virtual HangfireJob Job { get; set; }
    }
}
