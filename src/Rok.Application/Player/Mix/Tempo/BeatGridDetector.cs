namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Detects the tempo and the position of the first beat in a window of a track.</summary>
public static class BeatGridDetector
{
    /// <summary>Tells whether a mono signal is long enough for the tempo detection.</summary>
    /// <param name="sampleCount">Number of mono samples.</param>
    /// <param name="sampleRate">Sample rate of the signal, in Hz.</param>
    public static bool IsLongEnough(int sampleCount, int sampleRate) =>
        sampleRate > 0 && sampleCount >= MixThresholds.MinTempoSeconds * sampleRate;

    /// <summary>Detects the tempo and beat position from an onset curve already computed.</summary>
    /// <param name="curve">Onset curve of the window.</param>
    /// <param name="startSeconds">Absolute position in the track of the first sample.</param>
    /// <param name="knownBpm">Tempo already known (tag or earlier analysis); when set, only the phase is computed.</param>
    /// <returns>The detection; <see cref="TempoDetection.None"/> when the curve is not rhythmic enough.</returns>
    public static TempoDetection Detect(OnsetCurve curve, double startSeconds, double? knownBpm)
    {
        if (knownBpm is { } known and > 0)
            return DetectPhase(curve, known, 1, startSeconds);

        var estimate = TempoEstimator.Estimate(curve);

        if (estimate is null || estimate.Confidence < MixThresholds.MinBeatConfidence)
            return TempoDetection.None;

        var bpm = BeatPhaseEstimator.RefineTempo(curve, estimate.Bpm);

        return DetectPhase(curve, bpm, estimate.Confidence, startSeconds);
    }

    /// <summary>Detects the tempo and beat position of a mono window.</summary>
    /// <param name="mono">Mono samples of the window.</param>
    /// <param name="sampleRate">Sample rate of <paramref name="mono"/>, in Hz.</param>
    /// <param name="startSeconds">Absolute position in the track of the first sample.</param>
    /// <param name="knownBpm">Tempo already known (tag or earlier analysis); when set, only the phase is computed.</param>
    /// <returns>The detection; <see cref="TempoDetection.None"/> when the signal is too short or not rhythmic enough.</returns>
    public static TempoDetection Detect(ReadOnlySpan<float> mono, int sampleRate, double startSeconds, double? knownBpm)
    {
        if (!IsLongEnough(mono.Length, sampleRate))
            return TempoDetection.None;

        return Detect(OnsetCurve.Compute(mono, sampleRate), startSeconds, knownBpm);
    }

    private static TempoDetection DetectPhase(OnsetCurve curve, double bpm, double bpmConfidence, double startSeconds)
    {
        var phase = BeatPhaseEstimator.Estimate(curve, bpm);

        if (phase is null || phase.Confidence < MixThresholds.MinBeatConfidence)
            return new TempoDetection(bpm, bpmConfidence, null, phase?.Confidence ?? 0);

        return new TempoDetection(bpm, bpmConfidence, startSeconds + phase.OffsetSeconds, phase.Confidence);
    }
}