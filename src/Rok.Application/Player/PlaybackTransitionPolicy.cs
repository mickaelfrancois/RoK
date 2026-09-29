namespace Rok.Application.Player;

/// <summary>Decides how the player moves from one track to the next.</summary>
internal static class PlaybackTransitionPolicy
{
    /// <summary>
    /// Gapless when crossfade is disabled, when the sound is muted, or when <paramref name="next"/> is the
    /// following track of the same album; crossfade otherwise.
    /// </summary>
    public static EPlaybackTransition Decide(bool crossfadeEnabled, bool isMuted, TrackDto current, TrackDto next)
    {
        if (!crossfadeEnabled || isMuted || IsConsecutiveSameAlbum(current, next))
            return EPlaybackTransition.Gapless;

        return EPlaybackTransition.Crossfade;
    }

    internal static bool IsConsecutiveSameAlbum(TrackDto current, TrackDto next)
    {
        if (current.AlbumId is null || current.AlbumId != next.AlbumId)
            return false;

        if (current.TrackNumber is null || next.TrackNumber is null)
            return false;

        return next.TrackNumber == current.TrackNumber + 1;
    }
}