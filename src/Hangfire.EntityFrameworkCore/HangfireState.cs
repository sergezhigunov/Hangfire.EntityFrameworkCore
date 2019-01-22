using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public IReadOnlyDictionary<string, string> Data { get; set; }

        public virtual HangfireJob Job { get; set; }
    }
}
