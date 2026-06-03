using Rok.Application.Dto.Lyrics;

namespace Rok.ApplicationTests.Dto.Lyrics;

public class LyricsModelTests
{
    [Fact(DisplayName = "DisplayText should return the parsed plain lyrics when they are present")]
    public void DisplayText_ShouldReturnPlainLyrics_WhenPlainLyricsPresent()
    {
        // Arrange
        LyricsModel model = new()
        {
            PlainLyrics = "First line\nSecond line",
            SynchronizedLyrics = "[00:12.00]First line\n[00:15.00]Second line",
            LyricsType = ELyricsType.Synchronized
        };

        // Act
        string result = model.DisplayText;

        // Assert
        Assert.Equal("First line\nSecond line", result);
    }

    [Fact(DisplayName = "DisplayText should fall back to the raw file content when the parsed lyrics are empty")]
    public void DisplayText_ShouldFallBackToRawContent_WhenPlainLyricsEmpty()
    {
        // Arrange
        LyricsModel model = new()
        {
            PlainLyrics = string.Empty,
            SynchronizedLyrics = "[au: instrumental]",
            LyricsType = ELyricsType.Synchronized
        };

        // Act
        string result = model.DisplayText;

        // Assert
        Assert.Equal("[au: instrumental]", result);
    }

    [Fact(DisplayName = "DisplayText should return empty when there is nothing to display")]
    public void DisplayText_ShouldReturnEmpty_WhenNothingToDisplay()
    {
        // Arrange
        LyricsModel model = new()
        {
            PlainLyrics = string.Empty,
            SynchronizedLyrics = null,
            LyricsType = ELyricsType.None
        };

        // Act
        string result = model.DisplayText;

        // Assert
        Assert.Equal(string.Empty, result);
    }
}
