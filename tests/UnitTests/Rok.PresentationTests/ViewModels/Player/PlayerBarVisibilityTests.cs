using Rok.Application.Player;
using Rok.ViewModels.Player;

namespace Rok.PresentationTests.ViewModels.Player;

public class PlayerBarVisibilityTests
{
    [Theory(DisplayName = "lyrics_button_is_visible_only_for_music_with_lyrics")]
    [InlineData(EPlaybackMode.Music, true, true)]
    [InlineData(EPlaybackMode.Music, false, false)]
    [InlineData(EPlaybackMode.Radio, true, false)]
    [InlineData(EPlaybackMode.None, true, false)]
    public void IsLyricsButtonVisible_DependsOnModeAndLyrics(EPlaybackMode mode, bool lyricsExist, bool expected)
    {
        var result = PlayerBarVisibility.IsLyricsButtonVisible(mode, lyricsExist);

        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "lyrics_panel_is_visible_only_when_open_and_available")]
    [InlineData(true, EPlaybackMode.Music, true, true)]
    [InlineData(false, EPlaybackMode.Music, true, false)]
    [InlineData(true, EPlaybackMode.Radio, true, false)]
    [InlineData(true, EPlaybackMode.Music, false, false)]
    public void IsLyricsPanelVisible_DependsOnOpenStateAndAvailability(bool isOpen, EPlaybackMode mode, bool lyricsExist, bool expected)
    {
        var result = PlayerBarVisibility.IsLyricsPanelVisible(isOpen, mode, lyricsExist);

        Assert.Equal(expected, result);
    }
}