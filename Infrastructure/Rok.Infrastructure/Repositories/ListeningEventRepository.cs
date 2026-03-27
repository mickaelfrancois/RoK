using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Features.Insights.Query;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Infrastructure.Repositories;

public class ListeningEventRepository(IDbConnection connection, [FromKeyedServices("BackgroundConnection")] IDbConnection backgroundConnection, ILogger<ListeningEventRepository> logger) : GenericRepository<ListeningEventEntity>(connection, backgroundConnection, null, logger), IListeningEventRepository
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

        IEnumerable<RawListeningEvent> rows = await connection.QueryAsync<RawListeningEvent>(
            RawListeningEventsSql,
            new { previousStart, endDate });

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
            Badges = ComputeBadges(skipRate, replayRate, artistsPlayed, longSessionCount, globalPeakHour, fidelityRate)
        };
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

        // Skip category — most restrictive wins
        if (skipRate < 5)
            badges.Add(new BadgeDto(Badge.SmoothListener, "🎵"));
        else if (skipRate < 10)
            badges.Add(new BadgeDto(Badge.LowSkip, "✅"));
        else if (skipRate > 50)
            badges.Add(new BadgeDto(Badge.HyperZapper, "⚡⚡"));
        else if (skipRate > 30)
            badges.Add(new BadgeDto(Badge.Zapper, "⚡"));

        // Replay category
        if (replayRate > 25)
            badges.Add(new BadgeDto(Badge.Obsessed, "🔁"));
        else if (replayRate > 15)
            badges.Add(new BadgeDto(Badge.ReplayLover, "❤️"));
        else if (replayRate < 5)
            badges.Add(new BadgeDto(Badge.FreshSeeker, "🌱"));

        // Diversity category
        if (artistsPlayed > 30)
            badges.Add(new BadgeDto(Badge.Explorer, "🧭"));
        else if (artistsPlayed > 20)
            badges.Add(new BadgeDto(Badge.Curious, "🔍"));
        else if (artistsPlayed < 5)
            badges.Add(new BadgeDto(Badge.UltraFocus, "🎯"));
        else if (artistsPlayed < 10)
            badges.Add(new BadgeDto(Badge.RestrictedCircle, "🔒"));

        // Long session category
        if (longSessionCount > 15)
            badges.Add(new BadgeDto(Badge.DeepListener, "🎧"));
        else if (longSessionCount > 10)
            badges.Add(new BadgeDto(Badge.LongPlayer, "⏳"));
        else if (longSessionCount < 3)
            badges.Add(new BadgeDto(Badge.ShortSessions, "💫"));

        // Peak hour category — NightOwl takes priority over Nocturne for 0h–3h
        if (globalPeakHour >= 0)
        {
            if (globalPeakHour <= 3)
                badges.Add(new BadgeDto(Badge.NightOwl, "🦉"));
            else if (globalPeakHour >= 21)
                badges.Add(new BadgeDto(Badge.Nocturne, "🌙"));
            else if (globalPeakHour >= 6 && globalPeakHour <= 10)
                badges.Add(new BadgeDto(Badge.EarlyBird, "🌅"));
            else if (globalPeakHour >= 17 && globalPeakHour <= 20)
                badges.Add(new BadgeDto(Badge.Afterwork, "🌆"));
        }

        // Fidelity category
        if (fidelityRate > 35)
            badges.Add(new BadgeDto(Badge.UltraLoyal, "💎"));
        else if (fidelityRate > 20)
            badges.Add(new BadgeDto(Badge.Loyal, "🏅"));
        else if (fidelityRate < 10)
            badges.Add(new BadgeDto(Badge.Eclectic, "🌈"));

        return badges;
    }

    private static ListeningProfile ComputeProfile(double skipRate, double replayRate, int artistsPlayed, int longSessionCount, int globalPeakHour)
    {
        const int diversiteMax = 50;
        const int longSessionMax = 20;

        double skipRateNorm = skipRate / 100.0;
        double replayRateNorm = replayRate / 100.0;
        double diversiteNorm = Math.Min((double)artistsPlayed / diversiteMax, 1.0);
        double longSessionNorm = Math.Min((double)longSessionCount / longSessionMax, 1.0);
        double peakHourNorm = globalPeakHour < 0 ? 0.0
                            : (globalPeakHour >= 21 || globalPeakHour <= 2) ? 1.0
                            : globalPeakHour >= 18 ? 0.5
                            : 0.0;

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

    private sealed class ListeningSession
    {
        private readonly List<RawListeningEvent> _events = new();

        public DateTime LastPlayedAt => _events[^1].PlayedAt;
        public bool IsLong => _events.Count >= 5;

        public void Add(RawListeningEvent e) => _events.Add(e);
        public bool ContainsTrack(long trackId) => _events.Any(e => e.TrackId == trackId);
        public bool ContainsAlbum(long albumId) => _events.Any(e => e.AlbumId == albumId);
        public bool ContainsGenre(long genreId) => _events.Any(e => e.GenreId == genreId);
    }

    private sealed record RawListeningEvent
    {
        public long TrackId { get; init; }
        public long? ArtistId { get; init; }
        public long? AlbumId { get; init; }
        public long? GenreId { get; init; }
        public DateTime PlayedAt { get; init; }
        public bool WasSkipped { get; init; }
        public long DurationPlayed { get; init; }
        public string Title { get; init; } = string.Empty;
        public string ArtistName { get; init; } = string.Empty;
        public string AlbumName { get; init; } = string.Empty;
        public string GenreName { get; init; } = string.Empty;
    }
}