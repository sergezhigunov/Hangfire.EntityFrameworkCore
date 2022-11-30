using Hangfire.Annotations;

namespace Hangfire.EntityFrameworkCore;

internal interface ILockProvider
{
    void Acquire([NotNull] string resource, TimeSpan timeout);

    void Release([NotNull] string resource);
}
