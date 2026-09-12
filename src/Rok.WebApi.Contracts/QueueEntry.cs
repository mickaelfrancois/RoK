namespace Rok.WebApi.Contracts;

/// <summary>
/// A single entry of the playback queue returned by <c>GET /api/player/queue</c>.
/// </summary>
/// <param name="Index">The zero-based position of the entry within the queue.</param>
/// <param name="TrackId">The library identifier of the track.</param>
/// <param name="Title">The track title.</param>
/// <param name="ArtistName">The artist name.</param>
/// <param name="AlbumName">The album name.</param>
/// <param name="Duration">The track duration, in seconds.</param>
/// <param name="Score">The user rating, from 0 to 5.</param>
/// <param name="IsCurrent">Whether this entry is the track being played.</param>
public sealed record QueueEntry(
    int Index,
    long TrackId,
    string Title,
    string ArtistName,
    string AlbumName,
    long Duration,
    int Score,
    bool IsCurrent);