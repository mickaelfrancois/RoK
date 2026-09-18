namespace Rok.WebApi.Contracts;

/// <summary>
/// The track currently loaded in the player, as exposed to the web companion.
/// </summary>
/// <param name="TrackId">The library identifier of the track, or <c>0</c> when playing a radio stream.</param>
/// <param name="Title">The track title, or the stream title when playing a radio station.</param>
/// <param name="ArtistName">The artist name.</param>
/// <param name="AlbumName">The album name.</param>
/// <param name="GenreName">The genre name.</param>
/// <param name="Duration">The track duration, in seconds.</param>
/// <param name="Score">The user rating, from 0 to 5.</param>
/// <param name="ListenCount">How many times the track has been played.</param>
/// <param name="IsArtistFavorite">Whether the artist is flagged as a favorite.</param>
/// <param name="IsAlbumFavorite">Whether the album is flagged as a favorite.</param>
/// <param name="IsGenreFavorite">Whether the genre is flagged as a favorite.</param>
/// <param name="StationName">The radio station name when playing a stream, otherwise <c>null</c>.</param>
public sealed record NowPlaying(
    long TrackId,
    string Title,
    string ArtistName,
    string AlbumName,
    string GenreName,
    long Duration,
    int Score,
    int ListenCount,
    bool IsArtistFavorite,
    bool IsAlbumFavorite,
    bool IsGenreFavorite,
    string? StationName);