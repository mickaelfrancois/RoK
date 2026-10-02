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

        return TryPlanStretched(plan, outro, next, intro, trackLength, earliest)
            ?? TryPlanFromMixPoint(plan, outro, intro, trackLength, maxDurationSeconds, earliest)
            ?? TryAlignStart(plan, outro, intro, earliest)
            ?? plan;
    }

    private static MixPlan? TryPlanStretched(
        MixPlan basePlan,
        OutroCues outro,
        TrackDto next,
        IntroCues intro,
        double trackLength,
        double earliest)
    {
        if (outro.Beats is not { Bpm: > 0 } outBeats || intro.Beats is not { Bpm: > 0 } inBeats)
        {
            return null;
        }

        if (TempoMatch.StretchRatio(outBeats.Bpm, inBeats.Bpm, MixThresholds.MaxTempoStretch) is not { } ratio)
        {
            return null;
        }

        var barAligned = outBeats.FirstDownbeatSeconds != null && inBeats.FirstDownbeatSeconds != null;
        var outBar = MixThresholds.BeatsPerBar * 60 / outBeats.Bpm;
        var anchor = outBeats.FirstDownbeatSeconds ?? outBeats.FirstBeatSeconds;
        var incomingStart = inBeats.FirstDownbeatSeconds is { } downbeat && barAligned
            ? FirstIncomingBeat(downbeat, inBeats.BarPeriodSeconds, intro.MusicStartSeconds)
            : FirstIncomingBeat(inBeats.FirstBeatSeconds, 60 / inBeats.Bpm, intro.MusicStartSeconds);
        var returnSeconds = TempoStretchCurve.ReturnSeconds(inBeats.Bpm);

        foreach (var bars in new[] { MixThresholds.StretchMixBars, MixThresholds.StretchMixFallbackBars })
        {
            var duration = bars * outBar;

            if (duration > trackLength / 2)
            {
                continue;
            }

            if (next.Duration > 0 && next.Duration < incomingStart + (2 * (duration + returnSeconds)))
            {
                continue;
            }

            var mixPoint = StartFromMixPoint(outro, duration, earliest);
            var start = mixPoint?.Seconds ?? LatestBarStart(anchor, outBar, outro.MusicEndSeconds, duration, earliest);

            if (start is not { } startSeconds)
            {
                continue;
            }

            var alignment = new MixBeatAlignment(outBeats.Bpm, inBeats.Bpm, basePlan.StartSeconds - startSeconds, barAligned);

            return basePlan with
            {
                StartSeconds = startSeconds,
                DurationSeconds = duration,
                IncomingStartSeconds = incomingStart,
                BassSwapAtSeconds = NearestGridPointToMiddle(0, alignment, duration),
                Alignment = alignment,
                MixPointScore = mixPoint?.Score,
                Stretch = new MixTempoStretch(ratio, bars, returnSeconds)
            };
        }

        return null;
    }

    private static MixPoint? StartFromMixPoint(OutroCues outro, double duration, double earliest)
    {
        if (outro.MixPoint is not { } point || point.Score < MixThresholds.MinMixPointScore)
        {
            return null;
        }

        if (point.Seconds < earliest - Epsilon || point.Seconds + duration > outro.MusicEndSeconds + Epsilon)
        {
            return null;
        }

        return point;
    }

    private static double? LatestBarStart(double anchor, double barSeconds, double musicEnd, double duration, double earliest)
    {
        var start = anchor + (Math.Floor(((musicEnd - duration - anchor) / barSeconds) + Epsilon) * barSeconds);

        return start >= earliest - Epsilon ? start : null;
    }

    private static MixPlan? TryPlanFromMixPoint(
        MixPlan basePlan,
        OutroCues outro,
        IntroCues intro,
        double trackLength,
        int maxDurationSeconds,
        double earliest)
    {
        if (outro.MixPoint is not { } point || point.Score < MixThresholds.MinMixPointScore)
        {
            return null;
        }

        if (point.Seconds < earliest - Epsilon)
        {
            return null;
        }

        var duration = Math.Min(
            Math.Min(CrossfadeDuration.Clamp(maxDurationSeconds), trackLength / 2),
            outro.MusicEndSeconds - point.Seconds);

        if (duration < MixThresholds.MinMixSeconds)
        {
            return null;
        }

        var plan = basePlan with
        {
            StartSeconds = point.Seconds,
            DurationSeconds = duration,
            IncomingStartSeconds = intro.MusicStartSeconds,
            BassSwapAtSeconds = duration / 2,
            MixPointScore = point.Score
        };

        if (outro.Beats is not { Bpm: > 0 } outBeats || intro.Beats is not { Bpm: > 0 } inBeats
            || !TempoMatch.IsOctaveEquivalent(outBeats.Bpm, inBeats.Bpm, MixThresholds.BeatAlignTempoTolerance))
        {
            return plan;
        }

        var shift = basePlan.StartSeconds - point.Seconds;
        var incomingDownbeat = inBeats.FirstDownbeatSeconds;
        var barAligned = outBeats.FirstDownbeatSeconds != null && incomingDownbeat != null;
        var alignment = new MixBeatAlignment(outBeats.Bpm, inBeats.Bpm, shift, barAligned);
        var incomingStart = incomingDownbeat is { } downbeat && barAligned
            ? FirstIncomingBeat(downbeat, inBeats.BarPeriodSeconds, intro.MusicStartSeconds)
            : FirstIncomingBeat(inBeats.FirstBeatSeconds, 60 / inBeats.Bpm, intro.MusicStartSeconds);

        return plan with
        {
            IncomingStartSeconds = incomingStart,
            BassSwapAtSeconds = NearestGridPointToMiddle(0, alignment, duration),
            Alignment = alignment
        };
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
        var alignment = new MixBeatAlignment(outBeats.Bpm, inBeats.Bpm, shift);

        return plan with
        {
            StartSeconds = aligned,
            IncomingStartSeconds = incomingStart,
            BassSwapAtSeconds = NearestGridPointToMiddle(0, alignment, plan.DurationSeconds),
            Alignment = alignment
        };
    }

    private static double FirstIncomingBeat(double firstBeatSeconds, double periodSeconds, double musicStartSeconds)
    {
        var index = Math.Max(0, Math.Ceiling(((musicStartSeconds - firstBeatSeconds) / periodSeconds) - Epsilon));

        return firstBeatSeconds + (index * periodSeconds);
    }

    /// <summary>
    /// Returns the bass swap offset: the bar (when bar-aligned) or the beat of the outgoing track nearest to the middle
    /// of the mix, or the middle itself when none leaves room for the ramp. Shared by the planner and
    /// <see cref="ResolveAt"/>.
    /// </summary>
    private static double NearestGridPointToMiddle(double anchorSeconds, MixBeatAlignment alignment, double mixSeconds)
    {
        var middle = mixSeconds / 2;

        if (mixSeconds < BassSwapCurve.MinMixSeconds)
        {
            return middle;
        }

        var periodSeconds = alignment.AlignmentPeriodSeconds;
        var swapAt = anchorSeconds + (Math.Round((middle - anchorSeconds) / periodSeconds) * periodSeconds);
        var margin = BassSwapCurve.RampSeconds / 2;

        return swapAt < margin || swapAt > mixSeconds - margin ? middle : swapAt;
    }

    /// <summary>
    /// Returns the duration, the incoming start and the bass swap offset (from the start of the mix) to use when the
    /// cue is reached at <paramref name="position"/>. A beat-aligned plan reached late keeps the incoming track in
    /// phase and the bass swap on a beat (a bar when bar-aligned) of the outgoing track.
    /// </summary>
    public static (double Duration, double IncomingStart, double BassSwapAt) ResolveAt(MixPlan plan, double position)
    {
        var duration = plan.DurationSeconds;

        if (position > plan.StartSeconds + LateToleranceSeconds)
        {
            var end = plan.Stretch is null ? plan.MusicEndSeconds : plan.StartSeconds + plan.DurationSeconds;
            var remaining = end - position;
            duration = Math.Min(Math.Max(remaining, MixThresholds.MinMixSeconds), plan.DurationSeconds);
        }

        if (plan.Alignment is not { } alignment)
        {
            return (duration, plan.IncomingStartSeconds, duration / 2);
        }

        var period = alignment.AlignmentPeriodSeconds;
        var lateness = Math.Max(0, position - plan.StartSeconds);
        var incomingStart = plan.IncomingStartSeconds + ((lateness % period) * (plan.Stretch?.Ratio ?? 1));
        var anchor = PositiveModulo(plan.StartSeconds - position, period);

        return (duration, incomingStart, NearestGridPointToMiddle(anchor, alignment, duration));
    }

    private static double PositiveModulo(double value, double period)
    {
        var remainder = value % period;

        return remainder < 0 ? remainder + period : remainder;
    }
}