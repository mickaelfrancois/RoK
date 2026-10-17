using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Everything the database must record when the tagged fields of an album are edited, applied in one transaction.
/// </summary>
/// <param name="AlbumId">Edited album.</param>
/// <param name="Name">Final album name, in the form the import reads back.</param>
/// <param name="Year">Final album year; <c>null</c> keeps the current one.</param>
/// <param name="OldArtistId">Artist of the album before the edit.</param>
/// <param name="NewArtistId">Existing artist that takes the album; <c>null</c> when the artist is unchanged or created.</param>
/// <param name="NewArtistName">Name of the artist to create (the album then moves to it); <c>null</c> when no artist is created.</param>
/// <param name="OldGenreId">Genre of the album before the edit.</param>
/// <param name="NewGenreId">Existing genre that takes the album; <c>null</c> when the genre is unchanged or created.</param>
/// <param name="NewGenreName">Name of the genre to create (the album then moves to it); <c>null</c> when no genre is created.</param>
/// <param name="PendingWrites">Tracks whose file still has fields to write; stored in the same transaction.</param>
public sealed record AlbumEditDbChange(
    long AlbumId,
    string Name,
    int? Year,
    long OldArtistId,
    long? NewArtistId,
    string? NewArtistName,
    long? OldGenreId,
    long? NewGenreId,
    string? NewGenreName,
    IReadOnlyList<PendingTagWrite> PendingWrites);

/// <summary>
/// Final artist and genre of an edited album, created ones included.
/// </summary>
/// <param name="ArtistId">Artist of the album once the edit is applied.</param>
/// <param name="GenreId">Genre of the album once the edit is applied.</param>
public sealed record AlbumEditApplied(long ArtistId, long? GenreId);