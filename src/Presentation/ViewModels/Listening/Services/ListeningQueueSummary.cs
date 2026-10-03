namespace Rok.ViewModels.Listening.Services;

/// <summary>Remaining play time of the queue, and the local time at which it ends.</summary>
/// <param name="TrackCount">Number of tracks in the queue.</param>
/// <param name="TotalSeconds">Seconds left to play: the rest of the current track plus the tracks after it.</param>
/// <param name="EndTime">Local time at which the queue ends, or <c>null</c> when no track is playing.</param>
public sealed record ListeningQueueSummary(int TrackCount, long TotalSeconds, DateTimeOffset? EndTime)
{
    public static ListeningQueueSummary Compute(IReadOnlyList<long> durations, int currentIndex, double positionSeconds, DateTimeOffset localNow)
    {
        ArgumentNullException.ThrowIfNull(durations);

        if (currentIndex < 0 || currentIndex >= durations.Count)
        {
            return new ListeningQueueSummary(durations.Count, durations.Sum(), null);
        }

        var rest = durations.Skip(currentIndex + 1).Sum();
        var currentRest = Math.Max(0d, durations[currentIndex] - positionSeconds);
        var total = rest + (long)Math.Round(currentRest, MidpointRounding.AwayFromZero);

        return new ListeningQueueSummary(durations.Count, total, localNow.AddSeconds(total));
    }
}