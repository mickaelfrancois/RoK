namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// A field filled on both artists with different values. The kept artist's value wins.
/// </summary>
/// <param name="Field">Name of the artist property.</param>
/// <param name="TargetValue">Value of the kept artist (kept).</param>
/// <param name="SourceValue">Value of the merged artist (lost).</param>
public sealed record ArtistMergeFieldConflict(string Field, string? TargetValue, string? SourceValue);