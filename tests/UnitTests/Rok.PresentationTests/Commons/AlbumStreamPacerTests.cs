using Rok.Commons;

namespace Rok.PresentationTests.Commons;

public class AlbumStreamPacerTests
{
    [Fact(DisplayName = "DrainBatch should drain exactly batch size when the queue is longer")]
    public void DrainBatch_ShouldDrainExactlyBatchSize_WhenQueueIsLonger()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);
        Queue<int> pending = new(Enumerable.Range(1, 10));

        // Act
        IReadOnlyList<int> batch = pacer.DrainBatch(pending);

        // Assert
        Assert.Equal([1, 2, 3, 4], batch);
        Assert.Equal(6, pending.Count);
    }

    [Fact(DisplayName = "DrainBatch should drain everything when the queue is shorter than the batch")]
    public void DrainBatch_ShouldDrainEverything_WhenQueueIsShorterThanBatch()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);
        Queue<int> pending = new([1, 2]);

        // Act
        IReadOnlyList<int> batch = pacer.DrainBatch(pending);

        // Assert
        Assert.Equal([1, 2], batch);
        Assert.Empty(pending);
    }

    [Fact(DisplayName = "DrainBatch should return no item when the queue is empty")]
    public void DrainBatch_ShouldReturnNoItem_WhenQueueIsEmpty()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);
        Queue<int> pending = new();

        // Act
        IReadOnlyList<int> batch = pacer.DrainBatch(pending);

        // Assert
        Assert.Empty(batch);
    }

    [Fact(DisplayName = "next_returns_reveal_while_below_threshold_with_pending_items")]
    public void Next_ReturnsReveal_WhileBelowThresholdWithPendingItems()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);

        // Act
        AlbumStreamStep step = pacer.Next(displayedCount: 40, pendingCount: 8);

        // Assert
        Assert.Equal(AlbumStreamStep.Reveal, step);
    }

    [Fact(DisplayName = "next_returns_idle_when_nothing_is_pending_below_threshold")]
    public void Next_ReturnsIdle_WhenNothingIsPendingBelowThreshold()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);

        // Act
        AlbumStreamStep step = pacer.Next(displayedCount: 40, pendingCount: 0);

        // Assert
        Assert.Equal(AlbumStreamStep.Idle, step);
    }

    [Fact(DisplayName = "next_never_leaves_in_the_tick_that_reaches_the_threshold")]
    public void Next_NeverLeaves_InTheTickThatReachesTheThreshold()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);

        // Act
        AlbumStreamStep reachingTick = pacer.Next(displayedCount: 96, pendingCount: 20);
        AlbumStreamStep followingTick = pacer.Next(displayedCount: 100, pendingCount: 16);

        // Assert
        Assert.Equal(AlbumStreamStep.Reveal, reachingTick);
        Assert.Equal(AlbumStreamStep.Leave, followingTick);
    }

    [Fact(DisplayName = "next_returns_leave_once_then_done")]
    public void Next_ReturnsLeaveOnce_ThenDone()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);

        // Act
        AlbumStreamStep first = pacer.Next(displayedCount: 100, pendingCount: 0);
        AlbumStreamStep second = pacer.Next(displayedCount: 104, pendingCount: 4);

        // Assert
        Assert.Equal(AlbumStreamStep.Leave, first);
        Assert.Equal(AlbumStreamStep.Done, second);
    }

    [Fact(DisplayName = "request_leave_makes_the_next_tick_leave_without_revealing")]
    public void RequestLeave_MakesTheNextTickLeave_WithoutRevealing()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);
        pacer.RequestLeave();

        // Act
        AlbumStreamStep step = pacer.Next(displayedCount: 8, pendingCount: 12);

        // Assert
        Assert.Equal(AlbumStreamStep.Leave, step);
    }

    [Fact(DisplayName = "try_leave_now_succeeds_only_once")]
    public void TryLeaveNow_SucceedsOnlyOnce()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);

        // Act
        bool first = pacer.TryLeaveNow();
        bool second = pacer.TryLeaveNow();
        AlbumStreamStep step = pacer.Next(displayedCount: 100, pendingCount: 0);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(AlbumStreamStep.Done, step);
    }

    [Fact(DisplayName = "try_leave_now_fails_after_threshold_leave")]
    public void TryLeaveNow_Fails_AfterThresholdLeave()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 100);
        pacer.Next(displayedCount: 100, pendingCount: 0);

        // Act
        bool claimed = pacer.TryLeaveNow();

        // Assert
        Assert.False(claimed);
    }

    [Theory(DisplayName = "ProgressPercent should cap at 100 and scale on the displayed count")]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(140, 100, 100)]
    public void ProgressPercent_ShouldCapAt100_AndScaleOnDisplayedCount(int displayedCount, int unlockThreshold, double expected)
    {
        // Act
        double percent = AlbumStreamPacer.ProgressPercent(displayedCount, unlockThreshold);

        // Assert
        Assert.Equal(expected, percent);
    }
}