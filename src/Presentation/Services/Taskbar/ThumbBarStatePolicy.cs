using Rok.Application.Player;

namespace Rok.Services.Taskbar;

/// <summary>Decides the state of the thumbnail toolbar from the state of the player.</summary>
public static class ThumbBarStatePolicy
{
    public static ThumbBarState Compute(EPlaybackState state, bool canPrevious, bool canNext, bool hasCurrentMedia, int queueCount)
    {
        var hasQueue = queueCount > 0;

        return new ThumbBarState(
            state == EPlaybackState.Playing,
            state == EPlaybackState.Playing || hasCurrentMedia,
            canPrevious && hasQueue,
            canNext && hasQueue);
    }

    public static ThumbBarState From(IPlayerService player)
    {
        var hasCurrentMedia = player.CurrentTrack != null || player.CurrentStation != null;

        return Compute(player.PlaybackState, player.CanPrevious, player.CanNext, hasCurrentMedia, player.Playlist.Count);
    }
}