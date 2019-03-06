using System;
using Hangfire.States;

namespace Hangfire.EntityFrameworkCore
{
    internal static class StateExtensions
    {
        internal static DateTime? GetCreatedAt(this IState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            switch (state)
            {
                case ScheduledState scheduled:
                    return scheduled.ScheduledAt;

                case EnqueuedState enqueued:
                    return enqueued.EnqueuedAt;

                case ProcessingState processing:
                    return processing.StartedAt;

                case SucceededState succeeded:
                    return succeeded.SucceededAt;

                case FailedState failed:
                    return failed.FailedAt;

                case DeletedState deleted:
                    return deleted.DeletedAt;

                default:
                    return default;
            }
        }
    }
}
