using Hangfire.States;

namespace Hangfire.EntityFrameworkCore;

internal static class StateExtensions
{
    internal static DateTime? GetCreatedAt(this IState state)
    {
        if (state is null)
            throw new ArgumentNullException(nameof(state));

        return state switch
        {
            ScheduledState scheduled => scheduled.ScheduledAt,
            EnqueuedState enqueued => enqueued.EnqueuedAt,
            ProcessingState processing => processing.StartedAt,
            SucceededState succeeded => succeeded.SucceededAt,
            FailedState failed => failed.FailedAt,
            DeletedState deleted => deleted.DeletedAt,
            _ => default(DateTime?),
        };
    }
}
