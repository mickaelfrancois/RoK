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
        };
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