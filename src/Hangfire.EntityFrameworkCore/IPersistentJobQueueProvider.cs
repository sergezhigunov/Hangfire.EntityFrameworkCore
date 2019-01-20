namespace Hangfire.EntityFrameworkCore
{
    /// <summary>
    /// Provides Hangfire job queue manipulating and monitoring functionality.
    /// </summary>
    public interface IPersistentJobQueueProvider
    {
        /// <summary>
        /// Returns the Hangfire job queue. 
        /// </summary>
        /// <returns>
        ///  An instance of queue.
        /// </returns>
        IPersistentJobQueue GetJobQueue();

        /// <summary>
        /// Returns the Hangfire job monitoring API. 
        /// </summary>
        /// <returns>
        ///  An instance of queue monitoring API.
        /// </returns>
        IPersistentJobQueueMonitoringApi GetMonitoringApi();
    }
}
