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
            ToNowPlaying(player));


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