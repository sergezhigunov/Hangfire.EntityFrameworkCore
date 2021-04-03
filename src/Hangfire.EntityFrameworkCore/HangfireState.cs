using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireState
    {
        public long Id { get; set; }

        public long JobId { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; }

        public string Reason { get; set; }

        public DateTime CreatedAt { get; set; }

        [Required]
        public string Data { get; set; }

        public virtual HangfireJob Job { get; set; }
    }
}
