namespace Rok.Application.Features.Albums;

/// <summary>
/// Listening statistics of a single album, aggregated from listening events.
/// </summary>
public class AlbumListeningStatsDto
{
    /// <summary>Number of completed listens (at least half of the track played, not skipped).</summary>
    public int CompletedListenCount { get; set; }

    /// <summary>Total time spent listening to the album, in seconds (skipped events excluded).</summary>
    public long TotalDurationPlayedSeconds { get; set; }

    /// <summary>Date of the first non-skipped listen, null when the album was never listened.</summary>
    public DateTime? FirstListenedAt { get; set; }

    /// <summary>Date of the most recent non-skipped listen, null when the album was never listened.</summary>
    public DateTime? LastListenedAt { get; set; }

    /// <summary>Most active listening hour of the day, -1 when unknown.</summary>
    public int PeakHour { get; set; } = -1;

    /// <summary>Completed listens per month over the last twelve months, oldest first, zero-filled.</summary>
    public List<MonthlyListenCountDto> MonthlyListens { get; set; } = [];

    /// <summary>Ids of the album tracks having at least one completed listen.</summary>
    public List<long> ListenedTrackIds { get; set; } = [];

    public string PeakHourRange => PeakHour >= 0
        ? $"{PeakHour:D2}h - {(PeakHour + 3) % 24:D2}h"
        : string.Empty;
}

/// <summary>
/// Number of completed listens for one calendar month.
/// </summary>
public class MonthlyListenCountDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int Count { get; set; }
}
