using Rok.Application.Player;
using Rok.WebApi.Contracts;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Projects the application DTOs onto the wire contracts shared with the web companion.
/// Durations and positions are expressed in seconds throughout, matching <see cref="TrackDto.Duration"/>.
/// </summary>
internal static class WebApiMapper
{
    public static PlayerStatus ToStatus(IPlayerService player) =>
        new(player.PlaybackState.ToString(),
            player.Mode.ToString(),
            player.Volume,
            player.IsMuted,
            player.Position,
            player.CanNext,
            player.CanPrevious,
            player.CanSeek,
            player.IsLoopingEnabled,
            player.IsBuffering,
            player.Playlist.Count,
            ComputeQueueSignature(player.Playlist, player.CurrentTrack),
            ToNowPlaying(player));


    /// <summary>
    /// Fingerprints the queue: how many tracks, in which order, and where the playhead sits. Reordering a queue
    /// leaves its length and its playing track untouched, so this is the only signal telling a polling client
    /// that a shuffle happened — whether it was asked from the companion or from the desktop window.
    /// </summary>
    private static long ComputeQueueSignature(List<TrackDto> playlist, TrackDto? current)
    {
        long signature = playlist.Count;

        foreach (TrackDto track in playlist)
            signature = (signature * 31) + track.Id;

        // The playing entry is matched by reference: the same track may sit several times in one queue.
        int currentIndex = current is null ? -1 : playlist.IndexOf(current);

        return (signature * 31) + currentIndex + 1;
    }


    public static NowPlaying? ToNowPlaying(IPlayerService player)
    {
        TrackDto? track = player.CurrentTrack;

        if (track is null)
            return null;

        return new NowPlaying(
            track.Id,
            player.CurrentStreamTitle ?? track.Title,
            track.ArtistName,
            track.AlbumName,
            track.GenreName,
            track.Duration,
            track.Score,
            track.ListenCount,
            track.IsArtistFavorite,
            track.IsAlbumFavorite,
            track.IsGenreFavorite,
            player.CurrentStation?.Name);
    }


    /// <summary>
    /// Projects the whole loaded playlist, not only the upcoming tracks, so that the companion can render the
    /// full queue with the playing entry highlighted. The current entry is matched by reference because the same
    /// track identifier may legitimately appear several times in a queue.
    /// </summary>
    public static List<QueueEntry> ToQueue(IPlayerService player)
    {
        TrackDto? current = player.CurrentTrack;

        return [.. player.Playlist.Select((track, index) =>
            new QueueEntry(
                index,
                track.Id,
                track.Title,
                track.ArtistName,
                track.AlbumName,
                track.Duration,
                track.Score,
                ReferenceEquals(track, current)))];
    }


    public static LibraryTrack ToLibraryTrack(TrackDto track) =>
        new(track.Id,
            track.Title,
            track.ArtistName,
            track.AlbumName,
            track.AlbumId,
            track.GenreName,
            track.TrackNumber,
            track.Duration,
            track.Score,
            track.ListenCount);


    public static PlaylistSummary ToPlaylistSummary(PlaylistHeaderDto playlist) =>
        new(playlist.Id,
            playlist.Name,
            playlist.TrackCount,
            playlist.Duration,
            playlist.IsSmart,
            playlist.ShuffleOnPlay);
}