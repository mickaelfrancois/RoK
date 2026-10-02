namespace Rok.Application.Player.Mix.Tempo;

/// <summary>Finds which beat of the grid starts a bar, assuming 4/4 and an accent on the first beat.</summary>
public static class DownbeatEstimator
{
    /// <summary>Finds the first bar start of an analysed window.</summary>
    /// <param name="curve">Onset curve of the window.</param>
    /// <param name="curveStartSeconds">Absolute position in the track of the first sample of the curve.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <param name="firstBeatSeconds">Absolute position in the track of the first beat of the window.</param>
    /// <returns>The absolute position of the first bar start, or null when the curve is empty or flat.</returns>
    public static double? Estimate(OnsetCurve curve, double curveStartSeconds, double bpm, double firstBeatSeconds)
    {
        if (bpm <= 0 || curve.Values.Length == 0)
            return null;

        var period = 60.0 / bpm;
        var origin = firstBeatSeconds - curveStartSeconds;
        var end = curve.TimeOf(curve.Values.Length - 1);
        var bestShift = -1;
        var best = 0.0;

        for (var shift = 0; shift < MixThresholds.BeatsPerBar; shift++)
        {
            double sum = 0;
            var count = 0;

            for (var time = origin + (shift * period); time <= end; time += MixThresholds.BeatsPerBar * period)
            {
                sum += curve.ValueAt(time);
                count++;
            }

            if (count == 0)
                continue;

            var mean = sum / count;

            if (mean <= best)
                continue;

            best = mean;
            bestShift = shift;
        }

        return bestShift < 0 ? null : firstBeatSeconds + (bestShift * period);
    }
}