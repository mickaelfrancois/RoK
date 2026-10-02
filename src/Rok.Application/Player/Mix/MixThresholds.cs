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

    /// <summary>Sample rate the mono signal given to the tempo detector is brought close to.</summary>
    public const int TargetMonoRate = 11025;

    /// <summary>Size of an onset analysis frame, in samples (a power of two).</summary>
    public const int OnsetFrameSize = 256;

    /// <summary>Distance between two onset analysis frames, in samples.</summary>
    public const int OnsetHop = 64;

    /// <summary>Length of the sliding mean removed from the onset curve.</summary>
    public const double OnsetMeanSeconds = 0.5;

    /// <summary>Lowest tempo a detected BPM is folded into.</summary>
    public const double MinBpm = 70;

    /// <summary>Highest tempo a detected BPM is folded into.</summary>
    public const double MaxBpm = 180;

    /// <summary>Lowest tempo the autocorrelation looks for.</summary>
    public const double SearchMinBpm = 40;

    /// <summary>Highest tempo the autocorrelation looks for.</summary>
    public const double SearchMaxBpm = 250;

    /// <summary>
    /// Confidence under which no tempo and no beat grid is reported. Tuned with <c>bpm-check --sweep</c> on 485 tagged
    /// tracks: 0.15 keeps about 70 % of the windows with about 85 % of correct tempos.
    /// </summary>
    public const double MinBeatConfidence = 0.15;

    /// <summary>Shortest signal the tempo detector accepts.</summary>
    public const double MinTempoSeconds = 8;

    /// <summary>Lowest BPM tag taken into account.</summary>
    public const int MinTagBpm = 40;

    /// <summary>Highest BPM tag taken into account.</summary>
    public const int MaxTagBpm = 250;

    /// <summary>Fraction of the best autocorrelation peak a faster tempo must reach to be preferred.</summary>
    public const double OctavePeakRatio = 0.9;
}