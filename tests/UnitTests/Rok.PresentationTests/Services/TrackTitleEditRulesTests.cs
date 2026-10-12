using Rok.Services;

namespace Rok.PresentationTests.Services;

public class TrackTitleEditRulesTests
{
    [Theory(DisplayName = "cannot_save_when_candidate_is_empty_blank_or_unchanged")]
    [InlineData("Old", "")]
    [InlineData("Old", "   ")]
    [InlineData("Old", "Old")]
    [InlineData("Old", "  Old  ")]
    [InlineData("Old", null)]
    public void CanSave_ReturnsFalse(string current, string? candidate)
    {
        // Act
        bool result = TrackTitleEditRules.CanSave(current, candidate);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "can_save_when_candidate_differs_from_current_title")]
    [InlineData("Old", "New")]
    [InlineData("Old", "old")]
    [InlineData("Old", "  New ")]
    public void CanSave_ReturnsTrue(string current, string candidate)
    {
        // Act
        bool result = TrackTitleEditRules.CanSave(current, candidate);

        // Assert
        Assert.True(result);
    }

    [Fact(DisplayName = "normalize_trims_the_title")]
    public void Normalize_TrimsTitle()
    {
        // Act
        string result = TrackTitleEditRules.Normalize("  Hello  ");

        // Assert
        Assert.Equal("Hello", result);
    }

    [Fact(DisplayName = "preview_shows_old_title_then_new_title")]
    public void FormatPreview_ShowsOldThenNew()
    {
        // Act
        string result = TrackTitleEditRules.FormatPreview("Old", " New ");

        // Assert
        Assert.Equal("Old → New", result);
    }
}