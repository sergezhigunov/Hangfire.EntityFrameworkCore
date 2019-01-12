using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireList
    {
        [MaxLength(100)]
        public string Key { get; set; }

        public int Position { get; set; }

        public string Value { get; set; }

        public DateTime? ExpireAt { get; set; }
    }
}
