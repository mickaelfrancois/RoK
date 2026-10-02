namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Finds where the beats fall in an <see cref="OnsetCurve"/> once the tempo is known, with a comb filter.</summary>
public static class BeatPhaseEstimator
{
    private const double RefineRange = 0.03;
    private const double RefineStepBpm = 0.05;

    /// <summary>Position of the first beat and how sharply the curve supports it.</summary>
    /// <param name="OffsetSeconds">Time of the first beat, relative to the first sample of the analysed signal, in [0, period[.</param>
    /// <param name="Confidence">Contrast between the best and the average comb score, between 0 and 1.</param>
    public sealed record BeatPhase(double OffsetSeconds, double Confidence);

    /// <summary>Finds the phase of a beat grid of a given tempo.</summary>
    /// <param name="curve">Onset curve of the analysed window.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <returns>The phase, or null when the curve is too short or flat.</returns>
    public static BeatPhase? Estimate(OnsetCurve curve, double bpm)
    {
        if (bpm <= 0 || curve.Values.Length == 0)
            return null;

        var period = 60.0 / bpm;
        var step = curve.HopSeconds / 2;
        var steps = Math.Max(1, (int)Math.Round(period / step));
        var best = double.MinValue;
        var bestIndex = 0;
        double total = 0;

        for (var i = 0; i < steps; i++)
        {
            var score = Score(curve, i * step, period);
            total += score;

            if (score <= best)
                continue;

            best = score;
            bestIndex = i;
        }

        if (best <= 0)
            return null;

        var mean = total / steps;
        var confidence = Math.Clamp((best - mean) / best, 0, 1);

        return new BeatPhase(bestIndex * step, confidence);
    }

    /// <summary>
    /// Adjusts a tempo estimated from an autocorrelation (accurate to about a percent) so that the comb of beats
    /// stays aligned with the onsets along the whole window, which keeps the first beat position reliable.
    /// </summary>
    /// <param name="curve">Onset curve of the analysed window.</param>
    /// <param name="bpm">Initial tempo in beats per minute.</param>
    public static double RefineTempo(OnsetCurve curve, double bpm)
    {
        if (bpm <= 0 || curve.Values.Length == 0)
            return bpm;

        var from = bpm * (1 - RefineRange);
        var to = bpm * (1 + RefineRange);
        var bestBpm = bpm;
        var bestScore = double.MinValue;

        for (var candidate = from; candidate <= to; candidate += RefineStepBpm)
        {
            var period = 60.0 / candidate;
            var steps = Math.Max(1, (int)Math.Round(period / curve.HopSeconds));
            var score = double.MinValue;

            for (var i = 0; i < steps; i++)
                score = Math.Max(score, Score(curve, i * curve.HopSeconds, period));

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestBpm = candidate;
        }

        return bestBpm;
    }

    private static double Score(OnsetCurve curve, double offsetSeconds, double period)
    {
        var values = curve.Values;
        var last = values.Length - 1;
        double sum = 0;
        var count = 0;

        for (var time = offsetSeconds; ; time += period)
        {
            var position = (time - curve.FirstFrameCenterSeconds) / curve.HopSeconds;

            if (position > last)
                break;

            if (position < 0)
                continue;

            var index = (int)position;
            var fraction = position - index;
            var next = Math.Min(index + 1, last);

            sum += (values[index] * (1 - fraction)) + (values[next] * fraction);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }
}