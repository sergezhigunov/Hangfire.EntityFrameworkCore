using System.ComponentModel.DataAnnotations;

namespace Hangfire.EntityFrameworkCore
{
    internal class HangfireJobState
    {
        public long JobId { get; set; }
        public long StateId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Name { get; set; }

        public virtual HangfireJob Job { get; set; }

        public virtual HangfireState State { get; set; }
    }
}
