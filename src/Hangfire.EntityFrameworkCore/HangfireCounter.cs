using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireCounter
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; }

        public long Value { get; set; }

        public DateTime? ExpireAt { get; set; }
    }
}
