using Rok.Application.Player.Mix.Tempo;

namespace Rok.Application.Player.Mix;

/// <summary>
/// Finds, in the outro of a track, the bar boundary where a mix sounds the most natural: where the energy or the
/// spectrum changes (a section ends), where one bar is calmer than its neighbours (a breath), preferably a whole
/// number of 8-bar phrases after a strong change.
/// </summary>
public static class MixPointDetector
{
    private const double Epsilon = 1e-6;

    /// <summary>Scores the bar boundaries of the outro and returns the best one, whatever its score.</summary>
    /// <param name="envelope">Loudness envelope of the outro window.</param>
    /// <param name="curve">Onset curve of the same window.</param>
    /// <param name="curveStartSeconds">Absolute position in the track of the first sample of <paramref name="curve"/>.</param>
    /// <param name="grid">Beat grid of the outro; its first downbeat anchors the bars.</param>
    /// <param name="musicEndSeconds">Position where the music ends.</param>
    /// <returns>The best candidate, or null when the first downbeat is unknown or no boundary is admissible.</returns>
    public static MixPoint? Detect(RmsEnvelope envelope, OnsetCurve curve, double curveStartSeconds, BeatGrid grid, double musicEndSeconds)
    {
        if (grid.FirstDownbeatSeconds is not { } firstDownbeat || grid.Bpm <= 0)
            return null;

        var bar = grid.BarPeriodSeconds;
        var end = Math.Min(musicEndSeconds, envelope.StartSeconds + (envelope.LevelsDb.Length * envelope.WindowSeconds));
        var bars = (int)Math.Floor(((end - firstDownbeat) / bar) + Epsilon);

        if (bars < 2)
            return null;

        var energies = BarEnergies(envelope, firstDownbeat, bar, bars);
        var onsets = BarOnsets(curve, curveStartSeconds, firstDownbeat, bar, bars);
        var meanOnset = 0.0;

        for (var i = 0; i < bars; i++)
            meanOnset += onsets[i];

        meanOnset /= bars;

        var novelties = new double[bars];
        var anchor = -1;

        for (var j = 1; j < bars; j++)
        {
            novelties[j] = Novelty(energies, onsets, meanOnset, j);

            if (anchor < 0 || novelties[j] > novelties[anchor])
                anchor = j;
        }

        if (novelties[anchor] < MixThresholds.MixPointStrongRupture)
            anchor = -1;

        MixPoint? best = null;

        for (var j = 1; j < bars; j++)
        {
            var seconds = firstDownbeat + (j * bar);

            if (seconds + MixThresholds.MixPointMinRoomSeconds > musicEndSeconds)
                break;

            var score = ((MixThresholds.MixPointNoveltyWeight * novelties[j]) + (MixThresholds.MixPointDipWeight * Dip(energies, j)))
                * (1 + PhraseBonus(j, anchor));

            if (best is null || score > best.Score)
                best = new MixPoint(seconds, score);
        }

        return best;
    }

    private static double[] BarEnergies(RmsEnvelope envelope, double firstDownbeat, double bar, int bars)
    {
        var energies = new double[bars];
        var levels = envelope.LevelsDb;

        for (var j = 0; j < bars; j++)
        {
            var start = firstDownbeat + (j * bar) - envelope.StartSeconds;
            var from = Math.Clamp((int)Math.Round(start / envelope.WindowSeconds), 0, levels.Length);
            var to = Math.Clamp((int)Math.Round((start + bar) / envelope.WindowSeconds), 0, levels.Length);

            if (to <= from)
            {
                energies[j] = MixThresholds.SilentLevelDb;

                continue;
            }

            double power = 0;

            for (var i = from; i < to; i++)
                power += Math.Pow(10, levels[i] / 10.0);

            power /= to - from;
            energies[j] = power <= 0 ? MixThresholds.SilentLevelDb : 10 * Math.Log10(power);
        }

        return energies;
    }

    private static double[] BarOnsets(OnsetCurve curve, double curveStartSeconds, double firstDownbeat, double bar, int bars)
    {
        var onsets = new double[bars];

        for (var j = 0; j < bars; j++)
        {
            var start = firstDownbeat + (j * bar) - curveStartSeconds;
            onsets[j] = curve.MeanBetween(start, start + bar);
        }

        return onsets;
    }

    private static double Novelty(double[] energies, double[] onsets, double meanOnset, int boundary)
    {
        var bars = energies.Length;
        var beforeCount = Math.Min(MixThresholds.MixPointContextBars, boundary);
        var afterCount = Math.Min(MixThresholds.MixPointContextBars, bars - boundary);
        double energyBefore = 0;
        double energyAfter = 0;
        double onsetBefore = 0;
        double onsetAfter = 0;

        for (var i = boundary - beforeCount; i < boundary; i++)
        {
            energyBefore += energies[i];
            onsetBefore += onsets[i];
        }

        for (var i = boundary; i < boundary + afterCount; i++)
        {
            energyAfter += energies[i];
            onsetAfter += onsets[i];
        }

        var energyDelta = Math.Abs((energyAfter / afterCount) - (energyBefore / beforeCount));
        var onsetDelta = Math.Abs((onsetAfter / afterCount) - (onsetBefore / beforeCount)) / Math.Max(meanOnset, Epsilon);
        var energyNovelty = 1 - Math.Exp(-energyDelta / MixThresholds.MixPointNoveltyScaleDb);
        var onsetNovelty = 1 - Math.Exp(-onsetDelta);

        return Math.Max(energyNovelty, onsetNovelty);
    }

    private static double Dip(double[] energies, int bar)
    {
        if (bar + 1 >= energies.Length)
            return 0;

        var drop = Math.Min(energies[bar - 1], energies[bar + 1]) - energies[bar];

        return drop <= 0 ? 0 : 1 - Math.Exp(-drop / MixThresholds.MixPointDipScaleDb);
    }

    private static double PhraseBonus(int boundary, int anchor)
    {
        if (anchor < 0)
            return 0;

        var distance = boundary - anchor;

        if (distance < 8 || distance % 8 != 0)
            return 0;

        return (distance / 8) % 2 == 0 ? MixThresholds.MixPointPhrase16Bonus : MixThresholds.MixPointPhrase8Bonus;
    }
}