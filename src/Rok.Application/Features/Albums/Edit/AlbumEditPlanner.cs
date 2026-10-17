using Rok.Application.Features.Artists.Rename;
using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Application.Features.Albums.Edit;

/// <summary>
/// Pure planning of the tag writes of an album edit. Reads no database and no file.
/// </summary>
public static class AlbumEditPlanner
{
    /// <summary>
    /// Plans, for each track, the fields to write so that its file carries the edited values.
    /// The name goes to every file that does not carry it yet, the year to every file whose year differs,
    /// the artist only to the tracks of the album artist, the genre only to the tracks that carry the old album genre.
    /// </summary>
    /// <param name="subject">Album before the edit.</param>
    /// <param name="tracks">Tracks of the album.</param>
    /// <param name="files">Values currently read in the file of each track, by track id. A missing entry means the file is written.</param>
    /// <param name="newName">New album name.</param>
    /// <param name="newArtist">New album artist name.</param>
    /// <param name="newYear">New year, or null to keep it.</param>
    /// <param name="newGenre">New genre name, or null to keep it.</param>
    public static AlbumEditPlan Plan(
        AlbumEditSubject subject,
        IReadOnlyList<TrackEntity> tracks,
        IReadOnlyDictionary<long, TagFieldValues> files,
        string newName,
        string newArtist,
        int? newYear,
        string? newGenre)
    {
        AlbumEntity album = subject.Album;
        string name = AlbumNameRules.Normalize(newName);
        string artist = ArtistNameRules.Normalize(newArtist);
        string genre = AlbumNameRules.Normalize(newGenre);
        bool nameChanged = !AlbumNameRules.HasSameKey(album.Name, name);
        bool artistChanged = !ArtistNameRules.HasSameKey(subject.ArtistName, artist);
        bool yearChanged = newYear is not null && newYear != album.Year;
        bool genreChanged = !string.IsNullOrEmpty(genre) && !ArtistNameRules.HasSameKey(subject.GenreName, genre);

        List<PlannedTrackFields> writes = [];
        int otherArtist = 0;
        int genreTracks = 0;
        int genreKept = 0;

        foreach (TrackEntity track in tracks)
        {
            files.TryGetValue(track.Id, out TagFieldValues? file);

            bool ownsArtist = track.ArtistId == album.ArtistId;
            bool ownsGenre = track.GenreId == album.GenreId;

            if (artistChanged && !ownsArtist)
                otherArtist++;

            if (genreChanged)
            {
                if (ownsGenre)
                    genreTracks++;
                else
                    genreKept++;
            }

            ETagWriteFields fields = ETagWriteFields.None;
            string? changeAlbum = null;
            string? changeArtist = null;
            int? changeYear = null;
            string? changeGenre = null;

            if (nameChanged && (file is null || !AlbumNameRules.HasSameKey(file.Album, name)))
            {
                fields |= ETagWriteFields.Album;
                changeAlbum = name;
            }

            if (artistChanged && ownsArtist && (file is null || !ArtistNameRules.HasSameKey(file.Artist, artist)))
            {
                fields |= ETagWriteFields.Artist;
                changeArtist = artist;
            }

            if (yearChanged && (file is null || file.Year != newYear))
            {
                fields |= ETagWriteFields.Year;
                changeYear = newYear;
            }

            if (genreChanged && ownsGenre && (file is null || !ArtistNameRules.HasSameKey(file.Genre, genre)))
            {
                fields |= ETagWriteFields.Genre;
                changeGenre = genre;
            }

            if (fields == ETagWriteFields.None)
                continue;

            TagChanges changes = new() { Album = changeAlbum, Artist = changeArtist, Year = changeYear, Genre = changeGenre };
            writes.Add(new PlannedTrackFields(track, fields, changes));
        }

        return new AlbumEditPlan(writes, nameChanged, artistChanged, yearChanged, genreChanged, otherArtist, genreTracks, genreKept);
    }

    /// <summary>
    /// Tells whether a file is out of sync with the database: it carries neither the old nor the new album name,
    /// or, for a track of the album artist, neither the old nor the new artist. Only the changed fields are tested.
    /// Genre, year and disc never block.
    /// </summary>
    /// <param name="subject">Album before the edit.</param>
    /// <param name="track">Track owning the file.</param>
    /// <param name="current">Values currently read in the file, null when unreadable (never blocks here).</param>
    /// <param name="newName">New album name.</param>
    /// <param name="newArtist">New album artist name.</param>
    /// <returns>The blocker, or null when the file can be written.</returns>
    public static ArtistRenameBlocker? FindOutOfSync(AlbumEditSubject subject, TrackEntity track, TagFieldValues? current, string newName, string newArtist)
    {
        if (current is null)
            return null;

        AlbumEntity album = subject.Album;
        string name = AlbumNameRules.Normalize(newName);
        string artist = ArtistNameRules.Normalize(newArtist);

        if (!AlbumNameRules.HasSameKey(album.Name, name)
            && !AlbumNameRules.HasSameKey(current.Album, album.Name)
            && !AlbumNameRules.HasSameKey(current.Album, name))
        {
            return new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, current.Album);
        }

        if (track.ArtistId == album.ArtistId
            && !ArtistNameRules.HasSameKey(subject.ArtistName, artist)
            && !ArtistNameRules.HasSameKey(current.Artist, subject.ArtistName)
            && !ArtistNameRules.HasSameKey(current.Artist, artist))
        {
            return new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, current.Artist);
        }

        return null;
    }
}