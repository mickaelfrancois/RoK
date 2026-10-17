namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// A smart playlist whose result can change because of a merge.
/// </summary>
/// <param name="PlaylistId">Id of the playlist.</param>
/// <param name="Name">Name of the playlist.</param>
/// <param name="IsRewritten">True when a criterion naming the merged artist is rewritten.</param>
public sealed record SmartPlaylistMergeImpact(long PlaylistId, string Name, bool IsRewritten);