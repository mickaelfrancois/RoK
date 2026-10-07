using Rok.Application.Player;

namespace Rok.ViewModels.Player;

/// <summary>
/// Visibility rules of the lyrics button and panel of the player bar.
/// </summary>
public static class PlayerBarVisibility
{
    /// <summary>
    /// The lyrics button is shown only while playing music that has lyrics.
    /// </summary>
    /// <param name="mode">The current playback mode.</param>
    /// <param name="lyricsExist">Whether lyrics are available for the current track.</param>
    /// <returns><c>true</c> when the button must be visible.</returns>
    public static bool IsLyricsButtonVisible(EPlaybackMode mode, bool lyricsExist)
        => mode == EPlaybackMode.Music && lyricsExist;

    /// <summary>
    /// The lyrics panel is shown when the user opened it and lyrics are available.
    /// </summary>
    /// <param name="isOpen">Whether the user opened the panel.</param>
    /// <param name="mode">The current playback mode.</param>
    /// <param name="lyricsExist">Whether lyrics are available for the current track.</param>
    /// <returns><c>true</c> when the panel must be visible.</returns>
    public static bool IsLyricsPanelVisible(bool isOpen, EPlaybackMode mode, bool lyricsExist)
        => isOpen && IsLyricsButtonVisible(mode, lyricsExist);
}