namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// A queue statistics containing counts of enqueued and fetched jobs.
    /// </summary>
    public class QueueStatisticsDto
    {
        /// <summary>
        /// Returns the enqueued job count.
        /// </summary>
        public long Enqueued { get; set; }

        /// <summary>
        /// Returns the fetched job count.
        /// </summary>
        public long Fetched { get; set; }
    }
}
