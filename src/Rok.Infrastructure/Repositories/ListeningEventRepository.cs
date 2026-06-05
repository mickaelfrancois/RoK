using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Features.Insights;
using Rok.Application.Features.ListeningEvents;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class ListeningEventRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<ListeningEventRepository> logger, TimeProvider timeProvider) : GenericRepository<ListeningEventEntity>(connection, backgroundConnection, null, logger, timeProvider), IListeningEventRepository
{
    public override string GetTableName()
    {
        return "listeningevents";
    }

    public async Task<InsightsDto> GetInsightsAsync(DateTime month)
    {
        DateTime startDate = new(month.Year, month.Month, 1, 0, 0, 0, month.Kind);
        DateTime endDate = startDate.AddMonths(1).AddTicks(-1);
        DateTime previousStart = startDate.AddMonths(-1);

        IEnumerable<RawListeningEvent> rows = await _connection.QueryAsync<RawListeningEvent>(RawListeningEventsSql, new { previousStart, endDate });

        List<RawListeningEvent> allEvents = rows.ToList();
        List<RawListeningEvent> currentEvents = allEvents.Where(e => e.PlayedAt >= startDate).ToList();
        List<RawListeningEvent> previousEvents = allEvents.Where(e => e.PlayedAt < startDate).ToList();

        List<ListeningSession> currentSessions = DetectSessions(currentEvents);
        List<ListeningSession> previousSessions = DetectSessions(previousEvents);
        List<TrackInsightDto> topTracks = GetTopArtists(currentEvents, currentSessions);
        List<AlbumInsightDto> topAlbums = GetTopAlbums(currentEvents, currentSessions);
        List<GenreInsightDto> topGenres = GetTopGenres(currentEvents, currentSessions);
        List<HeatmapCellDto> heatmapCells = GetHeatmap(currentEvents);

        int artistsPlayed = currentEvents.Where(e => e.ArtistId.HasValue).Select(e => e.ArtistId).Distinct().Count();
        int longSessionCount = currentSessions.Count(s => s.IsLong);
        double skipRate = GetSkipRate(currentEvents);
        double replayRate = GetReplayRate(currentEvents);
        int globalPeakHour = GetGlobalPeakHour(currentEvents);
        double fidelityRate = GetFidelityRate(currentEvents);

        return new InsightsDto
        {
            SessionCount = currentSessions.Count,
            PreviousSessionCount = previousSessions.Count,
            PlayDuration = (int)currentEvents.Where(e => !e.WasSkipped).Sum(e => e.DurationPlayed),
            PreviousPlayDuration = (int)previousEvents.Where(e => !e.WasSkipped).Sum(e => e.DurationPlayed),
            TracksPlayed = currentEvents.Select(e => e.TrackId).Distinct().Count(),
            PreviousTracksPlayed = previousEvents.Select(e => e.TrackId).Distinct().Count(),
            ArtistsPlayed = currentEvents.Where(e => e.ArtistId.HasValue).Select(e => e.ArtistId).Distinct().Count(),
            PreviousArtistsPlayed = previousEvents.Where(e => e.ArtistId.HasValue).Select(e => e.ArtistId).Distinct().Count(),
            AlbumsPlayed = currentEvents.Where(e => e.AlbumId.HasValue).Select(e => e.AlbumId).Distinct().Count(),
            PreviousAlbumsPlayed = previousEvents.Where(e => e.AlbumId.HasValue).Select(e => e.AlbumId).Distinct().Count(),
            GenresPlayed = currentEvents.Where(e => e.GenreId.HasValue).Select(e => e.GenreId).Distinct().Count(),
            PreviousGenresPlayed = previousEvents.Where(e => e.GenreId.HasValue).Select(e => e.GenreId).Distinct().Count(),
            TopTracks = topTracks,
            TopAlbums = topAlbums,
            TopGenres = topGenres,
            HeatmapCells = heatmapCells,
            SkipRate = skipRate,
            ReplayRate = replayRate,
            LongSessionCount = longSessionCount,
            GlobalPeakHour = globalPeakHour,
            ListeningProfile = ComputeProfile(skipRate, replayRate, artistsPlayed, longSessionCount, globalPeakHour),
            Badges = ComputeBadges(skipRate, replayRate, artistsPlayed, longSessionCount, globalPeakHour, fidelityRate),
            SessionStats = GetSessionStats(currentSessions)
        };
    }

    public async Task<ListeningStatsDto> GetAlbumListeningStatsAsync(long albumId)
    {
        IEnumerable<RawScopedListeningEvent> rows = await _connection.QueryAsync<RawScopedListeningEvent>(AlbumListeningEventsSql, new { albumId });

        return BuildListeningStats(rows.ToList());
    }

    public async Task<ListeningStatsDto> GetArtistListeningStatsAsync(long artistId)
    {
        IEnumerable<RawScopedListeningEvent> rows = await _connection.QueryAsync<RawScopedListeningEvent>(ArtistListeningEventsSql, new { artistId });

        return BuildListeningStats(rows.ToList());
    }

    private ListeningStatsDto BuildListeningStats(List<RawScopedListeningEvent> events)
    {
        List<RawScopedListeningEvent> playedEvents = events.Where(e => !e.WasSkipped).ToList();
        List<RawScopedListeningEvent> completedEvents = playedEvents.Where(e => e.DurationPlayed * 2 >= e.DurationTotal).ToList();

        return new ListeningStatsDto
        {
            CompletedListenCount = completedEvents.Count,
            TotalDurationPlayedSeconds = playedEvents.Sum(e => e.DurationPlayed),
            FirstListenedAt = playedEvents.Count > 0 ? playedEvents.Min(e => e.PlayedAt) : null,
            LastListenedAt = playedEvents.Count > 0 ? playedEvents.Max(e => e.PlayedAt) : null,
            PeakHour = GetScopedPeakHour(playedEvents),
            MonthlyListens = GetMonthlyListens(completedEvents),
            ListenedTrackIds = completedEvents.Select(e => e.TrackId).Distinct().ToList(),
            ListenedAlbumIds = completedEvents.Where(e => e.AlbumId.HasValue).Select(e => e.AlbumId!.Value).Distinct().ToList()
        };
    }

    private static int GetScopedPeakHour(List<RawScopedListeningEvent> events)
    {
        if (events.Count == 0) return -1;
        return events
            .GroupBy(e => e.PlayedAt.Hour)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;
    }

    private List<MonthlyListenCountDto> GetMonthlyListens(List<RawScopedListeningEvent> completedEvents)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime currentMonth = new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        Dictionary<(int Year, int Month), int> counts = completedEvents
            .GroupBy(e => (e.PlayedAt.Year, e.PlayedAt.Month))
            .ToDictionary(g => g.Key, g => g.Count());

        List<MonthlyListenCountDto> months = new(12);
        for (int offset = 11; offset >= 0; offset--)
        {
            DateTime month = currentMonth.AddMonths(-offset);
            months.Add(new MonthlyListenCountDto
            {
                Year = month.Year,
                Month = month.Month,
                Count = counts.TryGetValue((month.Year, month.Month), out int count) ? count : 0
            });
        }

        return months;
    }

    private static double GetSkipRate(List<RawListeningEvent> events)
    {
        if (events.Count == 0) return 0;
        return (double)events.Count(e => e.WasSkipped) / events.Count * 100;
    }

    private static double GetReplayRate(List<RawListeningEvent> events)
    {
        if (events.Count == 0) return 0;
        int replayCount = 0;
        for (int i = 1; i < events.Count; i++)
        {
            RawListeningEvent current = events[i];
            for (int j = i - 1; j >= 0; j--)
            {
                RawListeningEvent previous = events[j];
                if ((current.PlayedAt - previous.PlayedAt).TotalHours > 2) break;
                if (previous.TrackId == current.TrackId)
                {
                    replayCount++;
                    break;
                }
            }
        }
        return (double)replayCount / events.Count * 100;
    }

    private static int GetGlobalPeakHour(List<RawListeningEvent> events)
    {
        if (events.Count == 0) return -1;
        return events
            .GroupBy(e => e.PlayedAt.Hour)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;
    }

    private static double GetFidelityRate(List<RawListeningEvent> events)
    {
        if (events.Count == 0) return 0;
        List<IGrouping<long, RawListeningEvent>> trackGroups = events.GroupBy(e => e.TrackId).ToList();
        if (trackGroups.Count == 0) return 0;
        int tracksPlayedMoreThan3Times = trackGroups.Count(g => g.Count() > 3);
        return (double)tracksPlayedMoreThan3Times / trackGroups.Count * 100;
    }

    private static List<BadgeDto> ComputeBadges(double skipRate, double replayRate, int artistsPlayed, int longSessionCount, int globalPeakHour, double fidelityRate)
    {
        List<BadgeDto> badges = new();

        if (GetSkipBadge(skipRate) is { } skipBadge) badges.Add(skipBadge);
        if (GetReplayBadge(replayRate) is { } replayBadge) badges.Add(replayBadge);
        if (GetDiversityBadge(artistsPlayed) is { } diversityBadge) badges.Add(diversityBadge);
        if (GetLongSessionBadge(longSessionCount) is { } sessionBadge) badges.Add(sessionBadge);
        if (GetPeakHourBadge(globalPeakHour) is { } peakBadge) badges.Add(peakBadge);
        if (GetFidelityBadge(fidelityRate) is { } fidelityBadge) badges.Add(fidelityBadge);

        return badges;
    }

    private static BadgeDto? GetSkipBadge(double skipRate)
    {
        if (skipRate < 5) return new BadgeDto(Badge.SmoothListener, "🎵");
        if (skipRate < 10) return new BadgeDto(Badge.LowSkip, "✅");
        if (skipRate > 50) return new BadgeDto(Badge.HyperZapper, "⚡⚡");
        if (skipRate > 30) return new BadgeDto(Badge.Zapper, "⚡");
        return null;
    }

    private static BadgeDto? GetReplayBadge(double replayRate)
    {
        if (replayRate > 25) return new BadgeDto(Badge.Obsessed, "🔁");
        if (replayRate > 15) return new BadgeDto(Badge.ReplayLover, "❤️");
        if (replayRate < 5) return new BadgeDto(Badge.FreshSeeker, "🌱");
        return null;
    }

    private static BadgeDto? GetDiversityBadge(int artistsPlayed)
    {
        if (artistsPlayed > 30) return new BadgeDto(Badge.Explorer, "🧭");
        if (artistsPlayed > 20) return new BadgeDto(Badge.Curious, "🔍");
        if (artistsPlayed < 5) return new BadgeDto(Badge.UltraFocus, "🎯");
        if (artistsPlayed < 10) return new BadgeDto(Badge.RestrictedCircle, "🔒");
        return null;
    }

    private static BadgeDto? GetLongSessionBadge(int longSessionCount)
    {
        if (longSessionCount > 15) return new BadgeDto(Badge.DeepListener, "🎧");
        if (longSessionCount > 10) return new BadgeDto(Badge.LongPlayer, "⏳");
        if (longSessionCount < 3) return new BadgeDto(Badge.ShortSessions, "💫");
        return null;
    }

    // NightOwl takes priority over Nocturne for 0h–3h
    private static BadgeDto? GetPeakHourBadge(int globalPeakHour)
    {
        if (globalPeakHour < 0) return null;
        if (globalPeakHour <= 3) return new BadgeDto(Badge.NightOwl, "🦉");
        if (globalPeakHour >= 21) return new BadgeDto(Badge.Nocturne, "🌙");
        if (globalPeakHour >= 6 && globalPeakHour <= 10) return new BadgeDto(Badge.EarlyBird, "🌅");
        if (globalPeakHour >= 17 && globalPeakHour <= 20) return new BadgeDto(Badge.Afterwork, "🌆");
        return null;
    }

    private static BadgeDto? GetFidelityBadge(double fidelityRate)
    {
        if (fidelityRate > 35) return new BadgeDto(Badge.UltraLoyal, "💎");
        if (fidelityRate > 20) return new BadgeDto(Badge.Loyal, "🏅");
        if (fidelityRate < 10) return new BadgeDto(Badge.Eclectic, "🌈");
        return null;
    }

    private static ListeningProfile ComputeProfile(double skipRate, double replayRate, int artistsPlayed, int longSessionCount, int globalPeakHour)
    {
        const int diversiteMax = 50;
        const int longSessionMax = 20;

        double skipRateNorm = skipRate / 100.0;
        double replayRateNorm = replayRate / 100.0;
        double diversiteNorm = Math.Min((double)artistsPlayed / diversiteMax, 1.0);
        double longSessionNorm = Math.Min((double)longSessionCount / longSessionMax, 1.0);

        double peakHourNorm;
        if (globalPeakHour < 0)
            peakHourNorm = 0.0;
        else if (globalPeakHour >= 21 || globalPeakHour <= 2)
            peakHourNorm = 1.0;
        else if (globalPeakHour >= 18)
            peakHourNorm = 0.5;
        else
            peakHourNorm = 0.0;

        double scoreExplorateur = (0.6 * diversiteNorm) + (0.2 * (1 - replayRateNorm)) + (0.2 * (1 - skipRateNorm));
        double scoreFidele = (0.5 * replayRateNorm) + (0.3 * longSessionNorm) + (0.2 * (1 - skipRateNorm));
        double scoreNocturne = peakHourNorm;
        double scoreFocus = (0.5 * longSessionNorm) + (0.3 * (1 - skipRateNorm)) + (0.2 * (1 - diversiteNorm));
        double scoreZappeur = (0.6 * skipRateNorm) + (0.2 * (1 - longSessionNorm)) + (0.2 * (1 - replayRateNorm));

        Dictionary<ListeningProfile, double> scores = new()
        {
            { ListeningProfile.CuriousExplorer, scoreExplorateur },
            { ListeningProfile.FaithfulIntense, scoreFidele },
            { ListeningProfile.NightOwl, scoreNocturne },
            { ListeningProfile.FocusMode, scoreFocus },
            { ListeningProfile.ChannelSurfer, scoreZappeur }
        };

        return scores.MaxBy(x => x.Value).Key;
    }

    private static List<TrackInsightDto> GetTopArtists(List<RawListeningEvent> currentEvents, List<ListeningSession> currentSessions)
    {
        int take = 5;

        return currentEvents
                    .Where(e => !e.WasSkipped)
                    .GroupBy(e => e.TrackId)
                    .OrderByDescending(g => g.Count())
                    .Take(take)
                    .Select(g =>
                    {
                        int peakHour = g.GroupBy(e => e.PlayedAt.Hour)
                                        .OrderByDescending(h => h.Count())
                                        .ThenBy(h => h.Key)
                                        .First().Key;
                        return new TrackInsightDto
                        {
                            Title = g.First().Title,
                            ArtistName = g.First().ArtistName,
                            PlayCount = g.Count(),
                            LongSessionCount = currentSessions.Count(s => s.IsLong && s.ContainsTrack(g.Key)),
                            PeakHour = peakHour
                        };
                    })
                    .ToList();
    }

    private static List<AlbumInsightDto> GetTopAlbums(List<RawListeningEvent> currentEvents, List<ListeningSession> currentSessions)
    {
        int take = 5;

        return currentEvents
                    .Where(e => !e.WasSkipped && e.AlbumId.HasValue)
                    .GroupBy(e => e.AlbumId!.Value)
                    .OrderByDescending(g => g.Count())
                    .Take(take)
                    .Select(g =>
                    {
                        int peakHour = g.GroupBy(e => e.PlayedAt.Hour)
                                        .OrderByDescending(h => h.Count())
                                        .ThenBy(h => h.Key)
                                        .First().Key;
                        return new AlbumInsightDto
                        {
                            Title = g.First().AlbumName,
                            ArtistName = g.First().ArtistName,
                            PlayCount = g.Count(),
                            LongSessionCount = currentSessions.Count(s => s.IsLong && s.ContainsAlbum(g.Key)),
                            PeakHour = peakHour,
                            DominantTitle = g.GroupBy(e => e.Title)
                                            .OrderByDescending(t => t.Count())
                                            .ThenBy(t => t.Key)
                                            .First().Key
                        };
                    })
                    .ToList();
    }

    private static List<GenreInsightDto> GetTopGenres(List<RawListeningEvent> currentEvents, List<ListeningSession> currentSessions)
    {
        int take = 3;

        List<RawListeningEvent> genreEvents = currentEvents
            .Where(e => !e.WasSkipped && e.GenreId.HasValue)
            .ToList();

        long totalSecondsAllGenres = genreEvents.Sum(e => e.DurationPlayed);

        return genreEvents
                    .GroupBy(e => e.GenreId!.Value)
                    .OrderByDescending(g => g.Count())
                    .Take(take)
                    .Select((g, index) =>
                    {
                        int peakHour = g.GroupBy(e => e.PlayedAt.Hour)
                                        .OrderByDescending(h => h.Count())
                                        .ThenBy(h => h.Key)
                                        .First().Key;
                        long genreSeconds = g.Sum(e => e.DurationPlayed);
                        return new GenreInsightDto
                        {
                            Rank = index + 1,
                            GenreName = g.First().GenreName,
                            PlayCount = g.Count(),
                            LongSessionCount = currentSessions.Count(s => s.IsLong && s.ContainsGenre(g.Key)),
                            PeakHour = peakHour,
                            DominantTitle = g.GroupBy(e => e.Title)
                                            .OrderByDescending(t => t.Count())
                                            .ThenBy(t => t.Key)
                                            .First().Key,
                            PlayPercentage = totalSecondsAllGenres > 0
                                ? (double)genreSeconds / totalSecondsAllGenres * 100
                                : 0
                        };
                    })
                    .ToList();
    }

    private static List<HeatmapCellDto> GetHeatmap(List<RawListeningEvent> currentEvents)
    {
        Dictionary<(int day, int hour), int> heatmapCounts = currentEvents
                    .GroupBy(e => (day: ((int)e.PlayedAt.DayOfWeek + 6) % 7, hour: e.PlayedAt.Hour))
                    .ToDictionary(g => g.Key, g => g.Count());

        List<HeatmapCellDto> heatmapCells = new();
        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                heatmapCells.Add(new HeatmapCellDto
                {
                    DayOfWeek = day,
                    Hour = hour,
                    Count = heatmapCounts.TryGetValue((day, hour), out int c) ? c : 0
                });
            }
        }

        return heatmapCells;
    }

    private static SessionStatsDto GetSessionStats(List<ListeningSession> sessions)
    {
        if (sessions.Count == 0)
            return new SessionStatsDto();

        long maxDuration = sessions.Max(s => s.TotalDurationSeconds);
        double averageTracks = sessions.Average(s => s.TrackCount);

        int nocturnalCount = sessions.Count(s => s.StartedAt.Hour >= 21 || s.StartedAt.Hour < 6);
        double nocturnalPercentage = (double)nocturnalCount / sessions.Count * 100;

        int mostCommonStartHour = sessions
            .GroupBy(s => s.StartedAt.Hour)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First().Key;

        ListeningSession mostIntenseSession = sessions
            .OrderByDescending(s => s.TrackCount)
            .ThenByDescending(s => s.TotalDurationSeconds)
            .First();

        IGrouping<int, ListeningSession> mostActiveDayGroup = sessions
            .GroupBy(s => ((int)s.StartedAt.DayOfWeek + 6) % 7)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First();

        return new SessionStatsDto
        {
            MaxDurationSeconds = maxDuration,
            AverageTracksPerSession = averageTracks,
            NocturnalSessionPercentage = nocturnalPercentage,
            MostCommonStartHour = mostCommonStartHour,
            MostIntenseSession = new IntenseSessionDto
            {
                DurationSeconds = mostIntenseSession.TotalDurationSeconds,
                TrackCount = mostIntenseSession.TrackCount,
                DominantGenre = mostIntenseSession.DominantGenre
            },
            MostActiveDayOfWeek = mostActiveDayGroup.Key,
            MostActiveDaySessionCount = mostActiveDayGroup.Count()
        };
    }

    private static List<ListeningSession> DetectSessions(List<RawListeningEvent> events)
    {
        List<ListeningSession> sessions = new();
        ListeningSession? current = null;

        foreach (RawListeningEvent e in events)
        {
            if (current is null || (e.PlayedAt - current.LastPlayedAt).TotalSeconds > 1800)
            {
                current = new ListeningSession();
                sessions.Add(current);
            }
            current.Add(e);
        }
        return sessions;
    }

    private const string RawListeningEventsSql =
        """
            SELECT
                le.TrackId,
                le.ArtistId,
                le.AlbumId,
                le.GenreId,
                le.PlayedAt,
                le.WasSkipped,
                le.DurationPlayed,
                t.Title,
                a.Name AS ArtistName,
                al.Name AS AlbumName,
                g.Name AS GenreName
            FROM ListeningEvents le
            INNER JOIN Tracks  t ON t.Id = le.TrackId
            LEFT  JOIN Artists a ON a.Id = le.ArtistId
            LEFT  JOIN Albums  al ON al.Id = le.AlbumId
            LEFT  JOIN Genres g ON g.Id = le.GenreId
            WHERE le.PlayedAt >= @previousStart
              AND le.PlayedAt <= @endDate
            ORDER BY le.PlayedAt ASC;
            """;

    private const string AlbumListeningEventsSql =
        """
            SELECT
                le.TrackId,
                le.AlbumId,
                le.PlayedAt,
                le.WasSkipped,
                le.DurationPlayed,
                le.DurationTotal
            FROM ListeningEvents le
            WHERE le.AlbumId = @albumId;
            """;

    // The artist scope covers events credited to the artist as track artist plus events on the
    // artist's discography: compilation tracks carry their own artist id on listening events.
    private const string ArtistListeningEventsSql =
        """
            SELECT
                le.TrackId,
                le.AlbumId,
                le.PlayedAt,
                le.WasSkipped,
                le.DurationPlayed,
                le.DurationTotal
            FROM ListeningEvents le
            LEFT JOIN Albums a ON a.Id = le.AlbumId
            WHERE le.ArtistId = @artistId
               OR a.ArtistId = @artistId;
            """;

    private sealed class ListeningSession
    {
        private readonly List<RawListeningEvent> _events = new();

        public DateTime StartedAt => _events[0].PlayedAt;
        public DateTime LastPlayedAt => _events[^1].PlayedAt;
        public bool IsLong => _events.Count >= 5;
        public int TrackCount => _events.Count;
        public long TotalDurationSeconds => _events.Where(e => !e.WasSkipped).Sum(e => e.DurationPlayed);
        public string DominantGenre => _events
            .Where(e => !string.IsNullOrEmpty(e.GenreName))
            .GroupBy(e => e.GenreName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? string.Empty;

        public void Add(RawListeningEvent e) => _events.Add(e);
        public bool ContainsTrack(long trackId) => _events.Any(e => e.TrackId == trackId);
        public bool ContainsAlbum(long albumId) => _events.Any(e => e.AlbumId == albumId);
        public bool ContainsGenre(long genreId) => _events.Any(e => e.GenreId == genreId);
    }

    private sealed record RawScopedListeningEvent
    {
        public long TrackId { get; set; } = default;
        public long? AlbumId { get; set; } = null;
        public DateTime PlayedAt { get; set; } = DateTime.MinValue;
        public bool WasSkipped { get; set; } = false;
        public long DurationPlayed { get; set; } = 0;
        public long DurationTotal { get; set; } = 0;
    }

    private sealed record RawListeningEvent
    {
        public long TrackId { get; set; } = default;
        public long? ArtistId { get; set; } = null;
        public long? AlbumId { get; set; } = null;
        public long? GenreId { get; set; } = null;
        public DateTime PlayedAt { get; set; } = DateTime.MinValue;
        public bool WasSkipped { get; set; } = false;
        public long DurationPlayed { get; set; } = 0;
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string GenreName { get; set; } = string.Empty;
    }
}