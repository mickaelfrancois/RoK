using System.Globalization;
using Rok.ViewModels.Listening.Services;

namespace Rok.PresentationTests.ViewModels.Listening.Services;

public class ListeningQueueSummaryFormatterTests
{
    private static readonly CultureInfo French = new("fr-FR");

    private static readonly ListeningSummaryLabels Labels = new(
        "{0} titre",
        "{0} titres",
        "{0} h {1} min",
        "{0} h",
        "{0} min",
        "Fin vers {0}");

    private static readonly DateTimeOffset EndAt = new(2026, 10, 3, 18, 5, 0, TimeSpan.Zero);

    [Fact(DisplayName = "format_renders_count_duration_and_end_time")]
    public void Format_RendersCountDurationAndEndTime()
    {
        // Arrange
        var summary = new ListeningQueueSummary(42, (2 * 3600) + (34 * 60), EndAt);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal("42 titres · 2 h 34 min · Fin vers 18:05", result);
    }

    [Fact(DisplayName = "format_uses_singular_for_one_track")]
    public void Format_UsesSingularForOneTrack()
    {
        // Arrange
        var summary = new ListeningQueueSummary(1, 600, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.StartsWith("1 titre · ", result);
    }

    [Fact(DisplayName = "format_shows_minutes_only_under_one_hour")]
    public void Format_ShowsMinutesOnlyUnderOneHour()
    {
        // Arrange
        var summary = new ListeningQueueSummary(3, 45 * 60, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal("3 titres · 45 min", result);
    }

    [Fact(DisplayName = "format_drops_zero_minutes_on_whole_hours")]
    public void Format_DropsZeroMinutesOnWholeHours()
    {
        // Arrange
        var summary = new ListeningQueueSummary(3, 7200, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal("3 titres · 2 h", result);
    }

    [Fact(DisplayName = "format_rounds_short_queue_up_to_one_minute")]
    public void Format_RoundsShortQueueUpToOneMinute()
    {
        // Arrange
        var summary = new ListeningQueueSummary(2, 20, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal("2 titres · 1 min", result);
    }

    [Fact(DisplayName = "format_omits_end_segment_without_separator")]
    public void Format_OmitsEndSegmentWithoutSeparator()
    {
        // Arrange
        var summary = new ListeningQueueSummary(2, 600, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal("2 titres · 10 min", result);
    }

    [Fact(DisplayName = "format_returns_empty_for_empty_queue")]
    public void Format_ReturnsEmpty_ForEmptyQueue()
    {
        // Arrange
        var summary = new ListeningQueueSummary(0, 0, null);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, Labels, French);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Theory(DisplayName = "format_uses_culture_short_time_pattern")]
    [InlineData("fr-FR", "18:05")]
    [InlineData("en-US", "6:05 PM")]
    public void Format_UsesCultureShortTimePattern(string cultureName, string expected)
    {
        // Arrange
        var culture = new CultureInfo(cultureName);
        var labels = Labels with { EndsAt = "{0}" };
        var summary = new ListeningQueueSummary(2, 600, EndAt);

        // Act
        var result = ListeningQueueSummaryFormatter.Format(summary, labels, culture);

        // Assert
        Assert.EndsWith(" · " + expected, result);
    }
}