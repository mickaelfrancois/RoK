namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// A field filled on both albums with different values. The kept album's value wins.
/// </summary>
/// <param name="Field">Name of the album property.</param>
/// <param name="TargetValue">Value of the kept album (kept).</param>
/// <param name="SourceValue">Value of the merged album (lost).</param>
public sealed record AlbumMergeFieldConflict(string Field, string? TargetValue, string? SourceValue);