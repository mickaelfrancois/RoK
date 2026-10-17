using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// The album as it is before the edit.
/// </summary>
/// <param name="Album">Album row.</param>
/// <param name="ArtistName">Name of the album artist, empty when none.</param>
/// <param name="GenreName">Name of the album genre, empty when none.</param>
public sealed record AlbumEditSubject(AlbumEntity Album, string ArtistName, string GenreName);

/// <summary>
/// Tag fields to write in the file of one track.
/// </summary>
/// <param name="Track">Track owning the file.</param>
/// <param name="Fields">Fields to write, kept as a pending write until the file is done.</param>
/// <param name="Changes">Tag changes to write.</param>
public sealed record PlannedTrackFields(TrackEntity Track, ETagWriteFields Fields, TagChanges Changes);

/// <summary>
/// What an album edit changes.
/// </summary>
/// <param name="Writes">Tracks whose file needs a write, in the order of the tracks.</param>
/// <param name="NameChanged">The album name changes.</param>
/// <param name="ArtistChanged">The album artist changes.</param>
/// <param name="YearChanged">The album year changes.</param>
/// <param name="GenreChanged">The album genre changes.</param>
/// <param name="OtherArtistTrackCount">Tracks of another artist, kept as they are when the album artist changes.</param>
/// <param name="GenreTrackCount">Tracks that carry the old album genre and take the new one.</param>
/// <param name="GenreKeptCount">Tracks that carry their own genre and keep it.</param>
public sealed record AlbumEditPlan(
    IReadOnlyList<PlannedTrackFields> Writes,
    bool NameChanged,
    bool ArtistChanged,
    bool YearChanged,
    bool GenreChanged,
    int OtherArtistTrackCount,
    int GenreTrackCount,
    int GenreKeptCount)
{
    /// <summary>
    /// True when at least one tagged field changes.
    /// </summary>
    public bool HasChanges => NameChanged || ArtistChanged || YearChanged || GenreChanged;
}