namespace Rok.WebApi.Contracts;

/// <summary>
/// What a surprise draw landed on, so the companion can tell the listener what just started.
/// </summary>
/// <param name="Kind">Either <c>album</c> or <c>artist</c>.</param>
/// <param name="Id">Identifier of the album or artist that was drawn.</param>
/// <param name="Name">Name of the album or artist.</param>
/// <param name="ArtistName">Artist behind the album; <c>null</c> when an artist was drawn.</param>
/// <param name="TrackCount">Number of tracks loaded into the queue.</param>
public sealed record SurprisePick(
    string Kind,
    long Id,
    string Name,
    string? ArtistName,
    int TrackCount);