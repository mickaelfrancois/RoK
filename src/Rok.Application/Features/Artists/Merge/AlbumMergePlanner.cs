using Rok.Domain.Entities;
using Rok.Shared.Extensions;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Pure grouping of the albums of two artists that the import would treat as one album.
/// </summary>
public static class AlbumMergePlanner
{
    /// <summary>
    /// Groups the non compilation albums of both artists by the import key (normalized name, invariant culture, ignoring case).
    /// The survivor is the kept artist's album of lowest id, else the merged artist's album of lowest id.
    /// Only groups with at least two albums are returned.
    /// A compilation listed in <paramref name="convertibleCompilationIds"/> becomes a normal album of the kept artist, so it is grouped like one.
    /// </summary>
    public static IReadOnlyList<AlbumMergeGroup> Plan(IEnumerable<AlbumEntity> targetAlbums, IEnumerable<AlbumEntity> sourceAlbums, IReadOnlySet<long>? convertibleCompilationIds = null)
    {
        Dictionary<string, List<(AlbumEntity Album, bool IsTarget)>> groups = new(StringComparer.InvariantCultureIgnoreCase);

        Collect(groups, targetAlbums, true, convertibleCompilationIds);
        Collect(groups, sourceAlbums, false, convertibleCompilationIds);

        List<AlbumMergeGroup> plan = [];

        foreach (List<(AlbumEntity Album, bool IsTarget)> members in groups.Values)
        {
            if (members.Count < 2)
                continue;

            plan.Add(BuildGroup(members));
        }

        return plan.OrderBy(g => g.SurvivorId).ToList();
    }

    private static void Collect(Dictionary<string, List<(AlbumEntity Album, bool IsTarget)>> groups, IEnumerable<AlbumEntity> albums, bool isTarget, IReadOnlySet<long>? convertibleCompilationIds)
    {
        foreach (AlbumEntity album in albums)
        {
            bool converted = album.IsCompilation && convertibleCompilationIds?.Contains(album.Id) == true;

            if (album.IsCompilation && !converted)
                continue;

            string key = album.Name.NormalizeIndexedName();

            if (!groups.TryGetValue(key, out List<(AlbumEntity Album, bool IsTarget)>? members))
            {
                members = [];
                groups[key] = members;
            }

            members.Add((album, isTarget));
        }
    }

    private static AlbumMergeGroup BuildGroup(List<(AlbumEntity Album, bool IsTarget)> members)
    {
        AlbumEntity survivor = members
            .OrderByDescending(m => m.IsTarget)
            .ThenBy(m => m.Album.Id)
            .First()
            .Album;

        List<long> mergedIds = members
            .Where(m => m.Album.Id != survivor.Id)
            .Select(m => m.Album.Id)
            .Order()
            .ToList();

        DateTime? lastListen = members
            .Select(m => m.Album.LastListen)
            .Max();

        return new AlbumMergeGroup(
            survivor.Id,
            mergedIds,
            survivor.Name,
            members.Any(m => m.Album.IsFavorite),
            members.Sum(m => m.Album.ListenCount),
            lastListen);
    }
}