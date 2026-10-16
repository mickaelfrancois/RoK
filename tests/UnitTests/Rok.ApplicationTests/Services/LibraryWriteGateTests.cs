using Rok.Application.Services;

namespace Rok.ApplicationTests.Services;

public class LibraryWriteGateTests
{
    private readonly LibraryWriteGate _gate = new();

    [Fact(DisplayName = "when_gate_is_free_try_enter_succeeds")]
    public void TryEnter_WhenFree_ReturnsLease()
    {
        // Act
        bool entered = _gate.TryEnter(out IDisposable? lease);

        // Assert
        Assert.True(entered);
        Assert.NotNull(lease);
    }

    [Fact(DisplayName = "when_gate_is_held_try_enter_fails_without_waiting")]
    public void TryEnter_WhenHeld_ReturnsFalse()
    {
        // Arrange
        _gate.TryEnter(out IDisposable? first);

        // Act
        bool entered = _gate.TryEnter(out IDisposable? second);

        // Assert
        Assert.NotNull(first);
        Assert.False(entered);
        Assert.Null(second);
    }

    [Fact(DisplayName = "when_lease_is_disposed_try_enter_succeeds_again")]
    public void TryEnter_AfterDispose_Succeeds()
    {
        // Arrange
        _gate.TryEnter(out IDisposable? first);
        first!.Dispose();

        // Act
        bool entered = _gate.TryEnter(out IDisposable? second);

        // Assert
        Assert.True(entered);
        Assert.NotNull(second);
    }

    [Fact(DisplayName = "when_gate_is_held_enter_async_waits_for_release")]
    public async Task EnterAsync_WhenHeld_WaitsForRelease()
    {
        // Arrange
        _gate.TryEnter(out IDisposable? first);

        // Act
        Task<IDisposable> waiting = _gate.EnterAsync(CancellationToken.None);
        await Task.Delay(50);
        bool completedBeforeRelease = waiting.IsCompleted;
        first!.Dispose();
        IDisposable lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(completedBeforeRelease);
        Assert.NotNull(lease);
    }

    [Fact(DisplayName = "when_enter_async_is_cancelled_while_held_it_throws")]
    public async Task EnterAsync_WhenCancelled_Throws()
    {
        // Arrange
        _gate.TryEnter(out _);
        using var cts = new CancellationTokenSource();

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using IDisposable lease = await _gate.EnterAsync(cts.Token);
        });
    }

    [Fact(DisplayName = "when_lease_is_disposed_twice_gate_is_released_once")]
    public void Dispose_Twice_ReleasesOnce()
    {
        // Arrange
        _gate.TryEnter(out IDisposable? first);
        first!.Dispose();
        _gate.TryEnter(out IDisposable? second);

        // Act
        first.Dispose();
        bool enteredWhileSecondHeld = _gate.TryEnter(out _);

        // Assert
        Assert.NotNull(second);
        Assert.False(enteredWhileSecondHeld);
    }
}