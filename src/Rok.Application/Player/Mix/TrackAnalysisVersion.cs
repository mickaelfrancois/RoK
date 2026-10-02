namespace Rok.Application.Player.Mix;

/// <summary>Version of the Mix analysis algorithm stored with each <c>trackAnalysis</c> row.</summary>
public static class TrackAnalysisVersion
{
    /// <summary>
    /// Current version. Any change to the cue detection, the tempo algorithm or a confidence threshold
    /// (for example <see cref="MixThresholds.MinBeatConfidence"/>) MUST increment it, so that stored rows are recomputed.
    /// </summary>
    public const int Current = 1;
}