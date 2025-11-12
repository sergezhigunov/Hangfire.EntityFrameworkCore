using Hangfire.States;
using Moq;

namespace Hangfire.EntityFrameworkCore.Tests;

public class StateExtensionsFacts
{
    [Fact]
    public static void GetCreatedAt_Throws_WhenDataParameterIsNull()
    {
        var state = default(IState);

        Assert.Throws<ArgumentNullException>(nameof(state),
            () => state.GetCreatedAt());
    }

    [Fact]
    public static void GetCreatedAt_ReturnsNull_WhenStateHasUnknownType()
    {
        var state = Mock.Of<IState>();

        var result = state.GetCreatedAt();

        Assert.Null(result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_ScheduledState()
    {
        var state = new ScheduledState(new TimeSpan(1, 0, 0));

        var result = state.GetCreatedAt();

        Assert.Equal(state.ScheduledAt, result);
        Assert.NotEqual(state.EnqueueAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_EnqueuedState()
    {
        var state = new EnqueuedState();

        var result = state.GetCreatedAt();

        Assert.Equal(state.EnqueuedAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_ProcessingState()
    {
        var state = ReflectionExtensions.CreateInstance<ProcessingState>(
            "serverId", "workerId");

        var result = state.GetCreatedAt();

        Assert.Equal(state.StartedAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_SucceededState()
    {
        var state = ReflectionExtensions.CreateInstance<SucceededState>(
            null, default(long), default(long));

        var result = state.GetCreatedAt();

        Assert.Equal(state.SucceededAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_FailedState()
    {
        var state = new FailedState(new Exception());

        var result = state.GetCreatedAt();

        Assert.Equal(state.FailedAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsCorrectValue_DeletedState()
    {
        var state = new DeletedState();

        var result = state.GetCreatedAt();

        Assert.Equal(state.DeletedAt, result);
    }

    [Fact]
    public static void GetCreatedAt_ReturnsNull_AwaitingState()
    {
        var state = new AwaitingState("1");

        var result = state.GetCreatedAt();

        Assert.Null(result);
    }
}
