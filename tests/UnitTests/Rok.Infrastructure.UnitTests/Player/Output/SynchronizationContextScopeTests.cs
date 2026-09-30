using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player.Output;

public class SynchronizationContextScopeTests
{
    private static T WithContext<T>(SynchronizationContext? context, Func<T> action)
    {
        SynchronizationContext? original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    [Fact(DisplayName = "run_detached_clears_the_context_and_restores_it_afterwards")]
    public void RunDetached_ClearsContext_AndRestoresItAfterwards()
    {
        // Arrange
        SynchronizationContext custom = new();

        // Act
        (SynchronizationContext? during, SynchronizationContext? after, int result) = WithContext(custom, () =>
        {
            SynchronizationContext? inside = null;
            int value = SynchronizationContextScope.RunDetached(() =>
            {
                inside = SynchronizationContext.Current;
                return 42;
            });

            return (inside, SynchronizationContext.Current, value);
        });

        // Assert
        Assert.Null(during);
        Assert.Same(custom, after);
        Assert.Equal(42, result);
    }

    [Fact(DisplayName = "run_detached_restores_the_context_when_the_action_throws")]
    public void RunDetached_RestoresContext_WhenActionThrows()
    {
        // Arrange
        SynchronizationContext custom = new();

        // Act
        (Exception? error, SynchronizationContext? after) = WithContext(custom, () =>
        {
            Exception? caught = Record.Exception(() => SynchronizationContextScope.RunDetached<int>(() => throw new InvalidOperationException("boom")));

            return (caught, SynchronizationContext.Current);
        });

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Same(custom, after);
    }

    [Fact(DisplayName = "run_detached_keeps_no_context_when_there_was_none")]
    public void RunDetached_KeepsNoContext_WhenThereWasNone()
    {
        // Act
        SynchronizationContext? after = WithContext(null, () =>
        {
            SynchronizationContextScope.RunDetached(() => 0);

            return SynchronizationContext.Current;
        });

        // Assert
        Assert.Null(after);
    }
}