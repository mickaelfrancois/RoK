namespace Rok.Application.Features.Albums.Merge;

/// <summary>
/// Two tracks, one on each album, that share the same disc and track number.
/// </summary>
/// <param name="Disc">Disc number (a missing disc counts as 1).</param>
/// <param name="TrackNumber">Track number on the disc.</param>
/// <param name="SourceTitle">Title of the track of the merged album.</param>
/// <param name="TargetTitle">Title of the track of the kept album.</param>
public sealed record DiscConflict(int Disc, int TrackNumber, string SourceTitle, string TargetTitle);