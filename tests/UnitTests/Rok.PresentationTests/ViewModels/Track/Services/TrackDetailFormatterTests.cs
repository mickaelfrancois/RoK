using System.Globalization;
using Rok.ViewModels.Track.Services;

namespace Rok.PresentationTests.ViewModels.Track.Services;

public class TrackDetailFormatterTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [Fact(DisplayName = "when_the_date_is_known_then_it_is_formatted_with_the_culture")]
    public void FormatDate_ShouldUseTheShortPatternOfTheCulture()
    {
        // Arrange
        DateTime date = new(2026, 10, 4, 13, 5, 0);

        // Act
        string result = TrackDetailFormatter.FormatDate(date, French);

        // Assert
        Assert.Equal(date.ToString("g", French), result);
    }

    [Fact(DisplayName = "when_the_date_is_unknown_then_a_dash_is_shown")]
    public void FormatDate_ShouldReturnADash_WhenTheDateIsUnknown()
    {
        // Arrange
        // Act
        string result = TrackDetailFormatter.FormatDate(null, French);

        // Assert
        Assert.Equal(TrackDetailFormatter.Missing, result);
    }

    [Theory(DisplayName = "when_the_text_is_empty_then_a_dash_is_shown")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatOptional_ShouldReturnADash_WhenTheTextIsEmpty(string? value)
    {
        Assert.Equal(TrackDetailFormatter.Missing, TrackDetailFormatter.FormatOptional(value));
    }

    [Fact(DisplayName = "when_the_text_has_content_then_it_is_returned_trimmed")]
    public void FormatOptional_ShouldTrimTheText()
    {
        Assert.Equal("abc-123", TrackDetailFormatter.FormatOptional("  abc-123 "));
    }

    [Fact(DisplayName = "when_summary_parts_are_missing_then_only_the_present_ones_are_joined")]
    public void JoinSummary_ShouldSkipEmptyParts()
    {
        Assert.Equal("03:22 · 8,1 MB", TrackDetailFormatter.JoinSummary("03:22", "", null, "8,1 MB"));
    }

    [Fact(DisplayName = "when_every_summary_part_is_missing_then_the_summary_is_empty")]
    public void JoinSummary_ShouldBeEmpty_WhenEveryPartIsMissing()
    {
        Assert.Equal(string.Empty, TrackDetailFormatter.JoinSummary("", null, " "));
    }
}