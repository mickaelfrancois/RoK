using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// What an album edit would do, shown before the user confirms.
/// </summary>
/// <param name="AlbumId">Id of the album; it never changes.</param>
/// <param name="OldName">Name of the album before the edit.</param>
/// <param name="NewName">Normalized name the album will carry.</param>
/// <param name="OldArtist">Artist of the album before the edit.</param>
/// <param name="NewArtist">Artist the album will belong to.</param>
/// <param name="CreatesArtist">True when the new artist does not exist yet and is created.</param>
/// <param name="OldYear">Year of the album before the edit.</param>
/// <param name="NewYear">Year the album will carry.</param>
/// <param name="OldGenre">Genre of the album before the edit.</param>
/// <param name="NewGenre">Genre the album will carry.</param>
/// <param name="CreatesGenre">True when the new genre does not exist yet and is created.</param>
/// <param name="FileCount">Number of files that receive at least one field.</param>
/// <param name="GenreTrackCount">Tracks that carry the old album genre and take the new one.</param>
/// <param name="GenreKeptCount">Tracks that carry their own genre and keep it.</param>
/// <param name="OtherArtistTrackCount">Tracks of another artist, kept as they are when the album artist changes.</param>
/// <param name="Collision">Another album the new name would be merged with, null otherwise.</param>
/// <param name="OrphanArtist">Old artist that would keep no track, null otherwise.</param>
/// <param name="Blockers">Reasons the edit cannot start. Empty when it can.</param>
public sealed record AlbumEditPreview(
    long AlbumId,
    string OldName,
    string NewName,
    string OldArtist,
    string NewArtist,
    bool CreatesArtist,
    int? OldYear,
    int? NewYear,
    string OldGenre,
    string NewGenre,
    bool CreatesGenre,
    int FileCount,
    int GenreTrackCount,
    int GenreKeptCount,
    int OtherArtistTrackCount,
    AlbumCollision? Collision,
    OrphanArtistWarning? OrphanArtist,
    IReadOnlyList<ArtistRenameBlocker> Blockers);

/// <summary>
/// Another album the new name and artist would be merged with.
/// </summary>
/// <param name="AlbumId">Id of the existing album.</param>
/// <param name="Name">Name of the existing album.</param>
/// <param name="ArtistName">Artist of the existing album.</param>
/// <param name="TrackCount">Number of tracks of the existing album.</param>
public sealed record AlbumCollision(long AlbumId, string Name, string ArtistName, int TrackCount);

/// <summary>
/// An artist that keeps no track once the album leaves it, and is removed by the next library cleaning.
/// </summary>
/// <param name="ArtistId">Id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
/// <param name="IsFavorite">True when the artist is a favorite.</param>
/// <param name="ListenCount">Listens of the artist, lost with it.</param>
public sealed record OrphanArtistWarning(long ArtistId, string Name, bool IsFavorite, int ListenCount);