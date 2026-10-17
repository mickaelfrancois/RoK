namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// Pure rule that finds the (disc, track number) collisions between two albums being merged.
/// </summary>
public static class AlbumDiscConflicts
{
    /// <summary>
    /// Lists the collisions and the disc offset that removes them when applied to the merged album tracks.
    /// A track without disc counts as disc 1; a track without number never collides.
    /// </summary>
    /// <param name="targetTracks">Tracks of the kept album.</param>
    /// <param name="sourceTracks">Tracks of the merged album.</param>
    public static AlbumDiscConflictsResult Find(IReadOnlyList<TrackEntity> targetTracks, IReadOnlyList<TrackEntity> sourceTracks)
    {
        Dictionary<(int Disc, int Number), string> targetByKey = [];

        foreach (TrackEntity track in targetTracks)
        {
            if (track.TrackNumber is not > 0)
                continue;

            targetByKey.TryAdd((DiscOf(track), track.TrackNumber.Value), track.Title);
        }

        List<DiscConflict> conflicts = [];

        foreach (TrackEntity track in sourceTracks)
        {
            if (track.TrackNumber is not > 0)
                continue;

            int disc = DiscOf(track);

            if (targetByKey.TryGetValue((disc, track.TrackNumber.Value), out string? targetTitle))
                conflicts.Add(new DiscConflict(disc, track.TrackNumber.Value, track.Title, targetTitle));
        }

        int offset = targetTracks.Count == 0 ? 0 : targetTracks.Max(DiscOf);

        return new AlbumDiscConflictsResult(conflicts, offset);
    }

    private static int DiscOf(TrackEntity track) => track.Disc is > 0 ? track.Disc.Value : 1;
}

/// <summary>
/// Collisions between two albums and the offset that resolves them.
/// </summary>
/// <param name="Conflicts">Colliding (disc, track number) pairs.</param>
/// <param name="Offset">Last disc of the kept album; added to each disc of the merged album to shift them.</param>
public sealed record AlbumDiscConflictsResult(IReadOnlyList<DiscConflict> Conflicts, int Offset);