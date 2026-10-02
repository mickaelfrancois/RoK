using Rok.Application.Dto;
using Rok.Application.Player.Mix.Tempo;

namespace Rok.Application.Player.Mix;

/// <summary>Computes when and how long a Mix transition lasts.</summary>
public static class MixTransitionPlanner
{
    private const double LateToleranceSeconds = 0.25;
    private const double Epsilon = 1e-9;

    /// <summary>Plans a transition ending on the music end of the current track, or null when there is no room to mix.</summary>
    /// <param name="current">Outgoing track.</param>
    /// <param name="outro">Cues of the outgoing track.</param>
    /// <param name="next">Incoming track.</param>
    /// <param name="intro">Cues of the incoming track.</param>
    /// <param name="trackLength">Length of the outgoing track in seconds.</param>
    /// <param name="maxDurationSeconds">Crossfade duration chosen in the options, used as a maximum.</param>
    public static MixPlan? Plan(
        TrackDto current,
        OutroCues outro,
        TrackDto next,
        IntroCues intro,
        double trackLength,
        int maxDurationSeconds)
    {
        var max = CrossfadeDuration.Clamp(maxDurationSeconds);
        var duration = outro.FadeOutSeconds > 0 ? Math.Min(max, outro.FadeOutSeconds) : max;
        duration = Math.Min(duration, trackLength / 2);

        var start = outro.MusicEndSeconds - duration;
        var earliest = trackLength - MixThresholds.AnalysisWindowSeconds;

        if (start < earliest)
        {
            start = earliest;
            duration = outro.MusicEndSeconds - start;
        }

        if (duration < MixThresholds.MinMixSeconds)
        {
            return null;
        }

        var plan = new MixPlan(current.Id, next.Id, start, duration, outro.MusicEndSeconds, intro.MusicStartSeconds, duration / 2);

        return TryAlignStart(plan, outro, intro, earliest) ?? plan;
    }

    private static MixPlan? TryAlignStart(MixPlan plan, OutroCues outro, IntroCues intro, double earliest)
    {
        if (outro.Beats is not { Bpm: > 0 } outBeats || intro.Beats is not { Bpm: > 0 } inBeats)
        {
            return null;
        }

        if (!TempoMatch.IsOctaveEquivalent(outBeats.Bpm, inBeats.Bpm, MixThresholds.BeatAlignTempoTolerance))
        {
            return null;
        }

        var outgoingPeriod = 60 / outBeats.Bpm;
        var incomingPeriod = 60 / inBeats.Bpm;
        var beatIndex = Math.Floor(((plan.StartSeconds - outBeats.FirstBeatSeconds) / outgoingPeriod) + Epsilon);

        if (beatIndex < 0)
        {
            return null;
        }

        var aligned = outBeats.FirstBeatSeconds + (beatIndex * outgoingPeriod);

        if (aligned < earliest - Epsilon)
        {
            return null;
        }

        var shift = Math.Max(0, plan.StartSeconds - aligned);
        var incomingStart = FirstIncomingBeat(inBeats.FirstBeatSeconds, incomingPeriod, intro.MusicStartSeconds);
        var swapAt = NearestBeatToMiddle(0, outgoingPeriod, plan.DurationSeconds);

        return plan with
        {
            StartSeconds = aligned,
            IncomingStartSeconds = incomingStart,
            BassSwapAtSeconds = swapAt,
            Alignment = new MixBeatAlignment(outBeats.Bpm, inBeats.Bpm, shift)
        };
    }

    private static double FirstIncomingBeat(double firstBeatSeconds, double periodSeconds, double musicStartSeconds)
    {
        var index = Math.Max(0, Math.Ceiling(((musicStartSeconds - firstBeatSeconds) / periodSeconds) - Epsilon));

        return firstBeatSeconds + (index * periodSeconds);
    }

    private static double NearestBeatToMiddle(double anchorSeconds, double periodSeconds, double mixSeconds)
    {
        var middle = mixSeconds / 2;

        if (mixSeconds < BassSwapCurve.MinMixSeconds)
        {
            return middle;
        }

        var swapAt = anchorSeconds + (Math.Round((middle - anchorSeconds) / periodSeconds) * periodSeconds);
        var margin = BassSwapCurve.RampSeconds / 2;

        return swapAt < margin || swapAt > mixSeconds - margin ? middle : swapAt;
    }

    /// <summary>
    /// Returns the duration, the incoming start and the bass swap offset (from the start of the mix) to use when the
    /// cue is reached at <paramref name="position"/>. A beat-aligned plan reached late keeps the incoming track in
    /// phase and the bass swap on a beat of the outgoing track.
    /// </summary>
    public static (double Duration, double IncomingStart, double BassSwapAt) ResolveAt(MixPlan plan, double position)
    {
        var duration = plan.DurationSeconds;

        if (position > plan.StartSeconds + LateToleranceSeconds)
        {
            var remaining = plan.MusicEndSeconds - position;
            duration = Math.Min(Math.Max(remaining, MixThresholds.MinMixSeconds), plan.DurationSeconds);
        }

        if (plan.Alignment is not { } alignment)
        {
            return (duration, plan.IncomingStartSeconds, duration / 2);
        }

        var period = alignment.OutgoingPeriodSeconds;
        var lateness = Math.Max(0, position - plan.StartSeconds);
        var incomingStart = plan.IncomingStartSeconds + (lateness % period);
        var anchor = PositiveModulo(plan.StartSeconds - position, period);

        return (duration, incomingStart, NearestBeatToMiddle(anchor, period, duration));
    }

    private static double PositiveModulo(double value, double period)
    {
        var remainder = value % period;

        return remainder < 0 ? remainder + period : remainder;
    }
}