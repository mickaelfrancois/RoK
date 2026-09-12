namespace Rok.WebApi.Contracts;

/// <summary>
/// A library track as returned by the playlist and search endpoints.
/// </summary>
/// <param name="Id">The track identifier.</param>
/// <param name="Title">The track title.</param>
/// <param name="ArtistName">The artist name.</param>
/// <param name="AlbumName">The album name.</param>
/// <param name="AlbumId">The album identifier, or <c>null</c> when the track has no album.</param>
/// <param name="GenreName">The genre name.</param>
/// <param name="TrackNumber">The track number within its album, when known.</param>
/// <param name="Duration">The track duration, in seconds.</param>
/// <param name="Score">The user rating, from 0 to 5.</param>
/// <param name="ListenCount">How many times the track has been played.</param>
public sealed record LibraryTrack(
    long Id,
    string Title,
    string ArtistName,
    string AlbumName,
    long? AlbumId,
    string GenreName,
    int? TrackNumber,
    long Duration,
    int Score,
    int ListenCount);