namespace Rok.Application.Player.Mix;

/// <summary>Tuning constants of the Mix mode cue detection.</summary>
public static class MixThresholds
{
    /// <summary>Length of the head or tail of a track that gets analysed.</summary>
    public const double AnalysisWindowSeconds = 30;

    /// <summary>Duration of one RMS window.</summary>
    public const double RmsWindowSeconds = 0.05;

    /// <summary>Absolute level below which a window is silence, unless the track is very quiet.</summary>
    public const double SilenceFloorDb = -50;

    /// <summary>Distance below the reference level under which a window is silence.</summary>
    public const double SilenceRelativeDb = 30;

    /// <summary>Lowest silence threshold ever used, and lowest level taken into account for the reference.</summary>
    public const double MinSilenceThresholdDb = -70;

    /// <summary>Percentile of the audible windows used as the body level of the track.</summary>
    public const double ReferencePercentile = 0.9;

    /// <summary>Length of the power average smoothing the envelope when looking for a fade.</summary>
    public const double FadeSmoothingSeconds = 1.0;

    /// <summary>Drop below the reference level at which a fade-out is considered started.</summary>
    public const double FadeStartDropDb = 1.5;

    /// <summary>Shortest fade-out considered natural.</summary>
    public const double MinNaturalFadeSeconds = 2;

    /// <summary>Smallest drop between the reference and the last audible level for a fade to be natural.</summary>
    public const double MinFadeDropDb = 15;

    /// <summary>Time the incoming track starts before its first audible window.</summary>
    public const double IntroPreRollSeconds = 0.1;

    /// <summary>Shortest duration of a mix recomputed late.</summary>
    public const double MinMixSeconds = 1;

    /// <summary>Level reported for a window of pure digital silence.</summary>
    public const double SilentLevelDb = -120;
}