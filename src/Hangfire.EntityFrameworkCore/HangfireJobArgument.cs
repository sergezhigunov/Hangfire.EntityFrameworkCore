using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireJobArgument
    {
        public long JobId { get; set; }

        public int Index { get; set; }

        [Required]
        [MaxLength(512)]
        public string ClrType { get; set; }

        public string Value { get; set; }


        public virtual HangfireJob Job { get; set; }
    }
}
