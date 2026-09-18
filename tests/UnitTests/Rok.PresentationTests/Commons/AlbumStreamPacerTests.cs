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

    [Fact(DisplayName = "ShouldUnlock should signal once when the batch crosses the threshold")]
    public void ShouldUnlock_ShouldSignalOnce_WhenBatchCrossesThreshold()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 10);

        // Act
        bool unlocked = pacer.ShouldUnlock(displayedCount: 12);

        // Assert
        Assert.True(unlocked);
    }

    [Fact(DisplayName = "ShouldUnlock should never signal again after the threshold is crossed")]
    public void ShouldUnlock_ShouldNeverSignalAgain_AfterThresholdIsCrossed()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 10);
        pacer.ShouldUnlock(displayedCount: 12);

        // Act
        bool unlockedAgain = pacer.ShouldUnlock(displayedCount: 16);

        // Assert
        Assert.False(unlockedAgain);
    }

    [Fact(DisplayName = "ShouldUnlock should stay silent below the threshold")]
    public void ShouldUnlock_ShouldStaySilent_BelowThreshold()
    {
        // Arrange
        AlbumStreamPacer pacer = new(batchSize: 4, unlockThreshold: 10);

        // Act
        bool unlocked = pacer.ShouldUnlock(displayedCount: 8);

        // Assert
        Assert.False(unlocked);
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