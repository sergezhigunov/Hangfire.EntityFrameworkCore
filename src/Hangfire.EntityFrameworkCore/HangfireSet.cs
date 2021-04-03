using System;
using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireSet : IExpirable
    {
        [MaxLength(100)]
        public string Key { get; set; }

        [MaxLength(256)]
        public string Value { get; set; }

        public double Score { get; set; }

        public DateTime? ExpireAt { get; set; }
    }
}
