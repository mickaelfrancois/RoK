using Rok.Application.Tag;
using Rok.Domain.Entities;
using Rok.Import.Models;
using Rok.Shared.Extensions;

namespace Rok.Import.Services;

/// <summary>
/// The album already known for a group of files sharing the same album tag in a folder.
/// </summary>
/// <param name="AlbumId">The identifier of the known album.</param>
/// <param name="WasCompilation">Whether the known tracks attached to the album had more than one distinct artist.</param>
/// <param name="FolderTrackIds">The identifiers of the known tracks of the group attached to the album.</param>
internal sealed record KnownFolderAlbum(long AlbumId, bool WasCompilation, IReadOnlySet<long> FolderTrackIds);

/// <summary>
/// Computes, from the import caches, the album each group of files was attached to before the rescan.
/// </summary>
internal static class FolderAlbumIdentity
{
    /// <summary>
    /// Snapshots the known album of each album-tag group. Read-only: nothing is written or mutated.
    /// </summary>
    /// <param name="files">The files of the folder.</param>
    /// <param name="tracks">The track cache.</param>
    /// <param name="albums">The album cache.</param>
    /// <returns>The known albums by album tag (case-insensitive). Groups without a known album are absent.</returns>
    public static IReadOnlyDictionary<string, KnownFolderAlbum> Resolve(IReadOnlyList<TrackFile> files, TrackImport tracks, AlbumImport albums)
    {
        Dictionary<string, KnownFolderAlbum> result = new(StringComparer.OrdinalIgnoreCase);

        IEnumerable<IGrouping<string, TrackFile>> groups = files
            .Where(file => !string.IsNullOrEmpty(file.Album))
            .GroupBy(file => file.Album.ToIndexKey(), StringComparer.Ordinal);

        foreach (IGrouping<string, TrackFile> group in groups)
        {
            string groupName = group.Key;

            List<TrackEntity> known = group
                .Select(file => tracks.GetFromCache(file.FullPath))
                .OfType<TrackEntity>()
                .Where(track => track.AlbumId.HasValue && HasSameName(albums.GetById(track.AlbumId.Value), groupName))
                .ToList();

            if (known.Count == 0)
                continue;

            long albumId = known
                .GroupBy(track => track.AlbumId!.Value)
                .OrderByDescending(albumGroup => albumGroup.Count())
                .ThenBy(albumGroup => albumGroup.Key)
                .First()
                .Key;

            List<TrackEntity> attached = known.Where(track => track.AlbumId == albumId).ToList();

            bool wasCompilation = attached
                .Where(track => track.ArtistId.HasValue)
                .Select(track => track.ArtistId!.Value)
                .Distinct()
                .Count() > 1;

            KnownFolderAlbum knownAlbum = new(albumId, wasCompilation, attached.Select(track => track.Id).ToHashSet());

            foreach (TrackFile file in group)
                result[file.Album] = knownAlbum;
        }

        return result;
    }


    private static bool HasSameName(AlbumCacheItem? album, string normalizedName)
    {
        return album is not null && album.Name.ToIndexKey() == normalizedName;
    }
}