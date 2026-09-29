using Rok.Application.Options;
using Rok.Application.Player;

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

    [Fact(DisplayName = "copy_from_preserves_replay_gain_options")]
    public void CopyFrom_PreservesReplayGainOptions()
    {
        // Arrange
        AppOptions source = new() { ReplayGainMode = EReplayGainMode.Track, ReplayGainPreampDb = 3.5 };
        AppOptions target = new();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(EReplayGainMode.Track, target.ReplayGainMode);
        Assert.Equal(3.5, target.ReplayGainPreampDb);
    }

    [Fact(DisplayName = "replay_gain_defaults_to_auto_without_preamp")]
    public void ReplayGain_DefaultsToAutoWithoutPreamp()
    {
        // Act
        AppOptions options = new();

        // Assert
        Assert.Equal(EReplayGainMode.Auto, options.ReplayGainMode);
        Assert.Equal(0, options.ReplayGainPreampDb);
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