namespace Rok.Application.Player.Mix;

/// <summary>Finds where the music starts and ends in a track, from its RMS envelope.</summary>
public static class MixCueDetector
{
    /// <summary>Detects the end of the music and a natural fade-out in the tail envelope of a track.</summary>
    /// <param name="envelope">Envelope of the end of the track.</param>
    /// <returns>The cues, or <c>null</c> when the envelope is empty or entirely silent.</returns>
    public static OutroCues? DetectOutro(RmsEnvelope envelope)
    {
        var levels = envelope.LevelsDb;

        if (!TryGetReferenceAndThreshold(levels, out var reference, out var threshold))
            return null;

        var lastAudible = -1;

        for (var i = levels.Length - 1; i >= 0; i--)
        {
            if (levels[i] > threshold)
            {
                lastAudible = i;
                break;
            }
        }

        if (lastAudible < 0)
            return null;

        var musicEnd = lastAudible == levels.Length - 1
            ? envelope.TrackLengthSeconds
            : envelope.StartSeconds + ((lastAudible + 1) * envelope.WindowSeconds);

        var fadeOut = DetectFadeOut(envelope, levels, lastAudible, reference, musicEnd);

        return new OutroCues(musicEnd, fadeOut);
    }

    /// <summary>Detects the start of the music in the head envelope of a track.</summary>
    /// <param name="envelope">Envelope of the start of the track.</param>
    /// <returns>The cues, or <c>null</c> when the envelope is empty or entirely silent.</returns>
    public static IntroCues? DetectIntro(RmsEnvelope envelope)
    {
        var levels = envelope.LevelsDb;

        if (!TryGetReferenceAndThreshold(levels, out _, out var threshold))
            return null;

        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i] <= threshold)
                continue;

            if (i == 0 && envelope.StartSeconds <= 0)
                return new IntroCues(0);

            var start = envelope.StartSeconds + (i * envelope.WindowSeconds) - MixThresholds.IntroPreRollSeconds;

            return new IntroCues(Math.Max(0, start));
        }

        return null;
    }

    private static double DetectFadeOut(RmsEnvelope envelope, float[] levels, int lastAudible, double reference, double musicEnd)
    {
        var smoothingWindows = Math.Max(1, (int)Math.Round(MixThresholds.FadeSmoothingSeconds / envelope.WindowSeconds));
        var smoothed = SmoothPower(levels, lastAudible, smoothingWindows);

        if (reference - smoothed[lastAudible] < MixThresholds.MinFadeDropDb)
            return 0;

        var limit = reference - MixThresholds.FadeStartDropDb;

        for (var i = lastAudible; i >= 0; i--)
        {
            if (smoothed[i] < limit)
                continue;

            var fadeStart = envelope.StartSeconds + ((i + 1) * envelope.WindowSeconds);
            var fadeOut = musicEnd - fadeStart;

            return fadeOut >= MixThresholds.MinNaturalFadeSeconds ? fadeOut : 0;
        }

        return 0;
    }

    private static double[] SmoothPower(float[] levels, int lastIndex, int windows)
    {
        var smoothed = new double[lastIndex + 1];
        var prefix = new double[lastIndex + 2];

        for (var i = 0; i <= lastIndex; i++)
            prefix[i + 1] = prefix[i] + Math.Pow(10, levels[i] / 10.0);

        for (var i = 0; i <= lastIndex; i++)
        {
            var from = Math.Max(0, i - windows + 1);
            var mean = (prefix[i + 1] - prefix[from]) / (i - from + 1);
            smoothed[i] = 10 * Math.Log10(Math.Max(mean, double.Epsilon));
        }

        return smoothed;
    }

    private static bool TryGetReferenceAndThreshold(float[] levels, out double reference, out double threshold)
    {
        reference = 0;
        threshold = 0;

        var audible = levels.Where(l => l > MixThresholds.MinSilenceThresholdDb).Order().ToArray();

        if (audible.Length == 0)
            return false;

        var index = Math.Clamp((int)Math.Ceiling(MixThresholds.ReferencePercentile * audible.Length) - 1, 0, audible.Length - 1);
        reference = audible[index];
        threshold = Math.Max(
            MixThresholds.MinSilenceThresholdDb,
            Math.Min(MixThresholds.SilenceFloorDb, reference - MixThresholds.SilenceRelativeDb));

        return true;
    }
}