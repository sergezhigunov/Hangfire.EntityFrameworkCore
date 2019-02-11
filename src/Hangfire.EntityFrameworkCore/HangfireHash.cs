using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireHash : IExpirable
    {
        [MaxLength(100)]
        public string Key { get; set; }

        [MaxLength(100)]
        public string Field { get; set; }

        public string Value { get; set; }

        public DateTime? ExpireAt { get; set; }
    }
}
