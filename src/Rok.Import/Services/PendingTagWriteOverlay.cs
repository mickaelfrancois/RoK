using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Domain.Enums;

namespace Rok.Import.Services;

/// <summary>
/// Replaces, in memory, the values read from files whose tags are still waiting to be rewritten
/// (after an album edit or merge) with the values already stored in the database.
/// </summary>
internal static class PendingTagWriteOverlay
{
    /// <summary>
    /// Overlays the pending fields of each file. Nothing is written to the files or to the database.
    /// </summary>
    /// <param name="files">The files of the folder, already read.</param>
    /// <param name="tracks">The track cache, which also holds the pending fields.</param>
    public static void Apply(IReadOnlyList<TrackFile> files, TrackImport tracks)
    {
        foreach (TrackFile file in files)
        {
            TrackEntity? track = tracks.GetFromCache(file.FullPath);

            if (track is null)
                continue;

            ETagWriteFields fields = tracks.GetPendingFields(track.Id);

            if (fields == ETagWriteFields.None)
                continue;

            Overlay(file, track, fields);
        }
    }


    private static void Overlay(TrackFile file, TrackEntity track, ETagWriteFields fields)
    {
        if (fields.HasFlag(ETagWriteFields.Album))
            file.Album = track.AlbumName;

        if (fields.HasFlag(ETagWriteFields.Artist))
            file.Artist = track.ArtistName;

        if (fields.HasFlag(ETagWriteFields.Genre))
            file.Genre = track.GenreName;

        if (fields.HasFlag(ETagWriteFields.Disc))
            file.Disc = track.Disc;
    }
}