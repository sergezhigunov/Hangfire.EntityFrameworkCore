using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireJobParameter
    {
        public long JobId { get; set; }

        [MaxLength(40)]
        public string Name { get; set; }

        public string Value { get; set; }

        public virtual HangfireJob Job { get; set; }
    }
}
