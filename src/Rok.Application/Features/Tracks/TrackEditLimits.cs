namespace Rok.Application.Features.Tracks;

/// <summary>
/// Bounds accepted when editing the tag fields of a track.
/// </summary>
public static class TrackEditLimits
{
    /// <summary>
    /// Highest accepted track number.
    /// </summary>
    public const int MaxTrackNumber = 999;

    /// <summary>
    /// Highest accepted disc number.
    /// </summary>
    public const int MaxDisc = 99;

    /// <summary>
    /// Highest accepted beats per minute.
    /// </summary>
    public const int MaxBpm = 999;
}