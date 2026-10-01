using Rok.Application.Dto;

namespace Rok.Application.Player.Mix;

/// <summary>Computes when and how long a Mix transition lasts.</summary>
public static class MixTransitionPlanner
{
    private const double LateToleranceSeconds = 0.25;

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

        return new MixPlan(current.Id, next.Id, start, duration, outro.MusicEndSeconds, intro.MusicStartSeconds);
    }

    /// <summary>Returns the duration and incoming start to use when the cue is reached at <paramref name="position"/>.</summary>
    public static (double Duration, double IncomingStart) ResolveAt(MixPlan plan, double position)
    {
        if (position <= plan.StartSeconds + LateToleranceSeconds)
        {
            return (plan.DurationSeconds, plan.IncomingStartSeconds);
        }

        var remaining = plan.MusicEndSeconds - position;
        var duration = Math.Min(Math.Max(remaining, MixThresholds.MinMixSeconds), plan.DurationSeconds);

        return (duration, plan.IncomingStartSeconds);
    }
}