using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class PlaybackTimeFormatterTests
{
    [Theory(DisplayName = "when_the_track_lasts_less_than_an_hour_then_the_time_is_formatted_as_minutes_and_seconds")]
    [InlineData(0, 0, "00:00")]
    [InlineData(42, 201, "00:42")]
    [InlineData(201, 201, "03:21")]
    public void Format_ShouldUseMinutesAndSeconds_WhenReferenceIsUnderOneHour(int valueSeconds, int referenceSeconds, string expected)
    {
        // Arrange
        TimeSpan value = TimeSpan.FromSeconds(valueSeconds);
        TimeSpan reference = TimeSpan.FromSeconds(referenceSeconds);

        // Act
        string result = PlaybackTimeFormatter.Format(value, reference);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "when_the_track_lasts_an_hour_or_more_then_the_hours_are_shown")]
    [InlineData(3600, 3600, "1:00:00")]
    [InlineData(3900, 3900, "1:05:00")]
    [InlineData(90062, 90062, "25:01:02")]
    public void Format_ShouldShowHours_WhenReferenceIsOneHourOrMore(int valueSeconds, int referenceSeconds, string expected)
    {
        // Arrange
        TimeSpan value = TimeSpan.FromSeconds(valueSeconds);
        TimeSpan reference = TimeSpan.FromSeconds(referenceSeconds);

        // Act
        string result = PlaybackTimeFormatter.Format(value, reference);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "when_the_track_lasts_over_an_hour_then_the_elapsed_time_uses_the_same_format")]
    public void Format_ShouldAlignElapsedTimeOnDuration_WhenReferenceIsOverOneHour()
    {
        // Arrange
        TimeSpan elapsed = TimeSpan.FromSeconds(201);
        TimeSpan duration = TimeSpan.FromMinutes(65);

        // Act
        string result = PlaybackTimeFormatter.Format(elapsed, duration);

        // Assert
        Assert.Equal("0:03:21", result);
    }

    [Fact(DisplayName = "when_the_value_exceeds_an_hour_but_the_duration_is_unknown_then_the_hours_are_kept")]
    public void Format_ShouldShowHours_WhenValueIsOverOneHourAndReferenceIsZero()
    {
        // Arrange
        TimeSpan value = TimeSpan.FromMinutes(65);
        TimeSpan reference = TimeSpan.Zero;

        // Act
        string result = PlaybackTimeFormatter.Format(value, reference);

        // Assert
        Assert.Equal("1:05:00", result);
    }

    [Fact(DisplayName = "when_the_value_is_negative_then_it_is_shown_as_zero")]
    public void Format_ShouldClampToZero_WhenValueIsNegative()
    {
        // Arrange
        TimeSpan value = TimeSpan.FromSeconds(-5);
        TimeSpan reference = TimeSpan.FromMinutes(3);

        // Act
        string result = PlaybackTimeFormatter.Format(value, reference);

        // Assert
        Assert.Equal("00:00", result);
    }
}