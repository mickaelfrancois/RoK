using Rok.Application.Dto.Lyrics;
using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class PlayerStateManagerTests
{
    private static PlayerStateManager BuildManager()
        => new(null!); // DispatcherQueue only used by ExecuteOnUIThread, not UpdateLyricsTime

    private static SyncLyricsModel BuildSyncLyrics(params (TimeSpan time, string lyric)[] lines)
    {
        SyncLyricsModel model = new();

        foreach ((TimeSpan time, string lyric) in lines)
        {
            model.Time.Add(time);
            model.Lyrics.Add(new LyricLine { Time = time, Lyric = lyric });
        }

        return model;
    }

    [Fact(DisplayName = "UpdateLyricsTime shows the active line when time falls between two timestamps")]
    public void UpdateLyricsTime_ShowsActiveLine_WhenTimeBetweenTimestamps()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(1), "line one"),
            (TimeSpan.FromSeconds(5), "line two")));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(3));

        // Assert
        Assert.Equal("line one", sut.CurrentLyric.Lyric);
    }

    [Fact(DisplayName = "UpdateLyricsTime shows the last line when time is past all timestamps")]
    public void UpdateLyricsTime_ShowsLastLine_WhenTimePastAllTimestamps()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(1), "line one"),
            (TimeSpan.FromSeconds(5), "line two"),
            (TimeSpan.FromSeconds(10), "line three")));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(15));

        // Assert
        Assert.Equal("line three", sut.CurrentLyric.Lyric);
    }

    [Fact(DisplayName = "UpdateLyricsTime shows the last line when time equals its exact timestamp")]
    public void UpdateLyricsTime_ShowsLastLine_WhenTimeEqualsLastTimestamp()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(1), "line one"),
            (TimeSpan.FromSeconds(5), "line two"),
            (TimeSpan.FromSeconds(10), "line three")));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal("line three", sut.CurrentLyric.Lyric);
    }

    [Fact(DisplayName = "UpdateLyricsTime resets to the correct line when seeking backward")]
    public void UpdateLyricsTime_ResetsToCorrectLine_WhenSeekingBackward()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(1), "line one"),
            (TimeSpan.FromSeconds(5), "line two"),
            (TimeSpan.FromSeconds(10), "line three")));
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(12));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(3));

        // Assert
        Assert.Equal("line one", sut.CurrentLyric.Lyric);
    }

    [Fact(DisplayName = "UpdateLyricsTime shows no lyric when time is before the first timestamp")]
    public void UpdateLyricsTime_ShowsNoLyric_WhenBeforeFirstTimestamp()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(5), "line one"),
            (TimeSpan.FromSeconds(10), "line two")));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(string.Empty, sut.CurrentLyric.Lyric);
    }

    [Fact(DisplayName = "UpdateLyricsTime seeks back to first line when seeking before the first timestamp")]
    public void UpdateLyricsTime_SeeksBackToNoLyric_WhenSeekingBeforeFirstTimestamp()
    {
        // Arrange
        PlayerStateManager sut = BuildManager();
        sut.SetSyncLyrics(BuildSyncLyrics(
            (TimeSpan.FromSeconds(5), "line one"),
            (TimeSpan.FromSeconds(10), "line two")));
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(8));

        // Act
        sut.UpdateLyricsTime(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(string.Empty, sut.CurrentLyric.Lyric);
    }
}