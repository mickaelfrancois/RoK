using Rok.Application.Options;

namespace Rok.ApplicationTests.Options;

public class AppOptionsTests
{
    [Fact(DisplayName = "copy_from_preserves_total_tracks_listened")]
    public void CopyFrom_PreservesTotalTracksListened()
    {
        // Arrange
        AppOptions source = new() { TotalTracksListened = 42 };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(42, target.TotalTracksListened);
    }

    [Fact(DisplayName = "copy_from_preserves_review_fields")]
    public void CopyFrom_PreservesReviewFields()
    {
        // Arrange
        DateTimeOffset lastPrompt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        AppOptions source = new() { SessionsCount = 7, HasRated = true, ReviewLastPromptDate = lastPrompt };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(7, target.SessionsCount);
        Assert.True(target.HasRated);
        Assert.Equal(lastPrompt, target.ReviewLastPromptDate);
    }
}