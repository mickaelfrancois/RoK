namespace Rok.Application.Player.Mix;

/// <summary>Version of the Mix analysis algorithm stored with each <c>trackAnalysis</c> row.</summary>
public static class TrackAnalysisVersion
{
    /// <summary>
    /// Current version. Any change to the cue detection, the tempo algorithm or a confidence threshold
    /// (for example <see cref="MixThresholds.MinBeatConfidence"/>, or any <c>MixThresholds.MixPoint*</c> constant)
    /// MUST increment it, so that stored rows are recomputed. <see cref="MixThresholds.MinMixPointScore"/> is the
    /// exception: the planner applies it to the stored best candidate.
    /// History: 1 = cues, tempo and beat phase; 2 = adds the downbeat and the best mix point.
    /// </summary>
    public const int Current = 2;
}