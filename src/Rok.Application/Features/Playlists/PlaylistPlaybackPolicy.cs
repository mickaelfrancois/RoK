using Rok.Shared.Enums;

namespace Rok.Application.Features.Playlists;

/// <summary>
/// Decides whether a smart playlist should regenerate its selection before playback
/// starts, based on the RefreshOnPlay flag and whether a specific track was clicked.
/// </summary>
public static class PlaylistPlaybackPolicy
{
    public static bool ShouldRegenerateBeforePlay(PlaylistHeaderDto playlist, bool trackClicked)
    {
        if (playlist.Type != (int)PlaylistType.Smart)
            return false;

        if (!playlist.RefreshOnPlay)
            return false;

        return !trackClicked;
    }
}