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

    /// <summary>Number of beats in a bar (the analysis assumes 4/4).</summary>
    public const int BeatsPerBar = 4;

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

    /// <summary>Relative tempo tolerance under which two tracks are beat-aligned (octave errors included).</summary>
    public const double BeatAlignTempoTolerance = 0.03;

    /// <summary>Number of bars compared on each side of a boundary when measuring its novelty.</summary>
    public const int MixPointContextBars = 2;

    /// <summary>Energy change, in dB, at which the novelty of a boundary reaches about 63 %.</summary>
    public const double MixPointNoveltyScaleDb = 6;

    /// <summary>Drop below the quietest neighbour bar, in dB, at which the dip of a boundary reaches about 63 %.</summary>
    public const double MixPointDipScaleDb = 6;

    /// <summary>Weight of the novelty in the score of a boundary.</summary>
    public const double MixPointNoveltyWeight = 0.6;

    /// <summary>Weight of the dip in the score of a boundary.</summary>
    public const double MixPointDipWeight = 0.4;

    /// <summary>Novelty from which the strongest boundary anchors the phrase grid.</summary>
    public const double MixPointStrongRupture = 0.5;

    /// <summary>Score bonus of a boundary an odd number of 8-bar phrases after the anchor.</summary>
    public const double MixPointPhrase8Bonus = 0.25;

    /// <summary>Score bonus of a boundary an even number of 8-bar phrases after the anchor.</summary>
    public const double MixPointPhrase16Bonus = 0.5;

    /// <summary>Room that must remain between a boundary and the end of the music, to stay clear of a natural fade.</summary>
    public const double MixPointMinRoomSeconds = 4;

    /// <summary>
    /// Score from which a mix point is used. Tuned with bpm-check on 485 outros: 46% are retained, and it stays clear of
    /// the peak of artefacts around 0.4. Applied by the planner; changing it does not require incrementing
    /// <see cref="TrackAnalysisVersion.Current"/> because the best candidate is stored whatever its score.
    /// </summary>
    public const double MinMixPointScore = 0.45;

    /// <summary>Fraction of the best autocorrelation peak a faster tempo must reach to be preferred.</summary>
    public const double OctavePeakRatio = 0.9;
}