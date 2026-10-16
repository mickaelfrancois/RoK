using Rok.Domain.Entities;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Final values of the kept artist after a merge.
/// </summary>
/// <param name="Merged">New entity carrying the kept artist's id and name and the merged field values.</param>
/// <param name="Conflicts">Fields filled on both artists with different values.</param>
public sealed record ArtistMergeResult(ArtistEntity Merged, IReadOnlyList<ArtistMergeFieldConflict> Conflicts);