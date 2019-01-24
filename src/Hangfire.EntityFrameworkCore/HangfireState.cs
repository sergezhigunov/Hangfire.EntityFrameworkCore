using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireState
    {
        public long Id { get; set; }

        public long JobId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string Reason { get; set; }

        public DateTime CreatedAt { get; set; }

        [Required]
        public IReadOnlyDictionary<string, string> Data { get; set; }

        public virtual HangfireJob Job { get; set; }
    }
}
