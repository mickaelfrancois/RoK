using Microsoft.Extensions.Time.Testing;
using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningQueueSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "compute_sums_remaining_tracks_and_rest_of_current_track")]
    public void Compute_SumsRemainingTracksAndRestOfCurrentTrack()
    {
        // Arrange
        long[] durations = [200, 300, 400];

        // Act
        var result = ListeningQueueSummary.Compute(durations, 1, 100, Now);

        // Assert
        Assert.Equal(3, result.TrackCount);
        Assert.Equal(600, result.TotalSeconds);
        Assert.Equal(Now.AddMinutes(10), result.EndTime);
    }

    [Fact(DisplayName = "compute_returns_no_end_time_without_current_track")]
    public void Compute_ReturnsNoEndTime_WithoutCurrentTrack()
    {
        // Arrange
        long[] durations = [200, 300, 400];

        // Act
        var result = ListeningQueueSummary.Compute(durations, -1, 0, Now);

        // Assert
        Assert.Null(result.EndTime);
        Assert.Equal(3, result.TrackCount);
        Assert.Equal(900, result.TotalSeconds);
    }

    [Fact(DisplayName = "compute_clamps_position_beyond_current_duration")]
    public void Compute_ClampsPositionBeyondCurrentDuration()
    {
        // Arrange
        long[] durations = [200, 300];

        // Act
        var result = ListeningQueueSummary.Compute(durations, 0, 999, Now);

        // Assert
        Assert.Equal(300, result.TotalSeconds);
    }

    [Fact(DisplayName = "compute_on_last_track_ends_with_current_track")]
    public void Compute_OnLastTrack_EndsWithCurrentTrack()
    {
        // Arrange
        long[] durations = [200, 300];

        // Act
        var result = ListeningQueueSummary.Compute(durations, 1, 100, Now);

        // Assert
        Assert.Equal(200, result.TotalSeconds);
        Assert.Equal(Now.AddSeconds(200), result.EndTime);
    }

    [Fact(DisplayName = "compute_uses_local_time_from_fake_time_provider")]
    public void Compute_UsesLocalTimeFromFakeTimeProvider()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));
        timeProvider.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test"));
        long[] durations = [600];

        // Act
        var result = ListeningQueueSummary.Compute(durations, 0, 0, timeProvider.GetLocalNow());

        // Assert
        Assert.Equal(TimeSpan.FromHours(2), result.EndTime!.Value.Offset);
        Assert.Equal(new DateTime(2026, 10, 3, 12, 10, 0), result.EndTime.Value.DateTime);
    }
}