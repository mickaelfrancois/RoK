namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Estimates a tempo from the autocorrelation of an <see cref="OnsetCurve"/>.</summary>
public static class TempoEstimator
{
    /// <summary>Estimates the tempo of an onset curve.</summary>
    /// <param name="curve">Onset curve of the analysed window.</param>
    /// <returns>The tempo and its confidence, or null when the curve is too short or flat.</returns>
    public static TempoEstimate? Estimate(OnsetCurve curve)
    {
        var values = curve.Values;
        var hop = curve.HopSeconds;
        var lagMin = (int)Math.Ceiling(60.0 / (MixThresholds.SearchMaxBpm * hop));
        var lagMax = (int)Math.Floor(60.0 / (MixThresholds.SearchMinBpm * hop));

        if (values.Length < (lagMax * 2) || lagMin < 1)
            return null;

        var centered = Center(values);
        var autocorrelation = Autocorrelate(centered, lagMax);
        var zeroLag = autocorrelation[0];

        if (zeroLag <= 0)
            return null;

        var mean = 0.0;

        for (var lag = lagMin; lag <= lagMax; lag++)
            mean += autocorrelation[lag];

        mean /= lagMax - lagMin + 1;

        var best = double.MinValue;

        for (var lag = lagMin; lag <= lagMax; lag++)
        {
            if (IsPeak(autocorrelation, lag))
                best = Math.Max(best, Interpolate(autocorrelation, lag).Height);
        }

        if (best <= 0)
            return null;

        var threshold = best * MixThresholds.OctavePeakRatio;

        for (var lag = lagMin; lag <= lagMax; lag++)
        {
            if (!IsPeak(autocorrelation, lag))
                continue;

            var peak = Interpolate(autocorrelation, lag);

            if (peak.Height < threshold)
                continue;

            var bpm = Fold(60.0 / (peak.Lag * hop));
            var confidence = Math.Clamp((peak.Height - mean) / (zeroLag - mean), 0, 1);

            return new TempoEstimate(bpm, confidence);
        }

        return null;
    }

    /// <summary>Brings a tempo into the usual range by doubling or halving it.</summary>
    /// <param name="bpm">Tempo in beats per minute.</param>
    public static double Fold(double bpm)
    {
        if (bpm <= 0 || double.IsNaN(bpm) || double.IsInfinity(bpm))
            return bpm;

        while (bpm > MixThresholds.MaxBpm)
            bpm /= 2;

        while (bpm < MixThresholds.MinBpm)
            bpm *= 2;

        return bpm;
    }

    private static double[] Center(float[] values)
    {
        double sum = 0;

        foreach (var value in values)
            sum += value;

        var mean = sum / values.Length;
        var centered = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
            centered[i] = values[i] - mean;

        return centered;
    }

    private static double[] Autocorrelate(double[] x, int maxLag)
    {
        var result = new double[maxLag + 2];

        for (var lag = 0; lag <= maxLag + 1; lag++)
        {
            double sum = 0;
            var count = x.Length - lag;

            for (var i = 0; i < count; i++)
                sum += x[i] * x[i + lag];

            result[lag] = sum / count;
        }

        return result;
    }

    private static bool IsPeak(double[] autocorrelation, int lag) =>
        autocorrelation[lag] > 0
        && autocorrelation[lag] >= autocorrelation[lag - 1]
        && autocorrelation[lag] > autocorrelation[lag + 1];

    private static (double Lag, double Height) Interpolate(double[] autocorrelation, int lag)
    {
        var before = autocorrelation[lag - 1];
        var at = autocorrelation[lag];
        var after = autocorrelation[lag + 1];
        var denominator = before - (2 * at) + after;

        if (denominator >= 0)
            return (lag, at);

        var delta = Math.Clamp(0.5 * (before - after) / denominator, -1, 1);

        return (lag + delta, at - (0.25 * (before - after) * delta));
    }
}