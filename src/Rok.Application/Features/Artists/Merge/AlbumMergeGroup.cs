namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Albums of the same name that end up as one album.
/// </summary>
/// <param name="SurvivorId">Album that stays.</param>
/// <param name="MergedIds">Albums folded into the survivor, then deleted.</param>
/// <param name="Name">Name of the survivor.</param>
/// <param name="IsFavorite">Favorite flag of the survivor after the merge.</param>
/// <param name="ListenCount">Listen count of the survivor after the merge.</param>
/// <param name="LastListen">Last listen of the survivor after the merge.</param>
public sealed record AlbumMergeGroup(
    long SurvivorId,
    IReadOnlyList<long> MergedIds,
    string Name,
    bool IsFavorite,
    int ListenCount,
    DateTime? LastListen)
{
    /// <summary>
    /// Builds the group for an explicit pair: <paramref name="target"/> stays, <paramref name="source"/> is folded into it.
    /// </summary>
    /// <param name="target">Album that stays.</param>
    /// <param name="source">Album that is merged then deleted.</param>
    public static AlbumMergeGroup ForPair(AlbumEntity target, AlbumEntity source)
    {
        DateTime? lastListen = target.LastListen is null || source.LastListen is null
            ? target.LastListen ?? source.LastListen
            : target.LastListen > source.LastListen ? target.LastListen : source.LastListen;

        return new AlbumMergeGroup(
            target.Id,
            [source.Id],
            target.Name,
            target.IsFavorite || source.IsFavorite,
            target.ListenCount + source.ListenCount,
            lastListen);
    }

    /// <summary>
    /// Summary shown in the merge preview.
    /// </summary>
    public AlbumMergeGroupPreview ToPreview() => new(Name, MergedIds.Count + 1);
}

/// <summary>
/// One merged album group as shown to the user.
/// </summary>
/// <param name="Name">Album name.</param>
/// <param name="AlbumCount">Number of albums that become one.</param>
public sealed record AlbumMergeGroupPreview(string Name, int AlbumCount);