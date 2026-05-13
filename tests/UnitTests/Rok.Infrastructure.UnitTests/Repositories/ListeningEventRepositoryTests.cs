using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Insights.Query;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class ListeningEventRepositoryTests
{
    private static readonly DateTime CurrentMonth = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SqliteDatabaseFixture CreateFixture() => new();

    private static ListeningEventRepository CreateRepository(SqliteDatabaseFixture fixture)
        => new(fixture.Connection, fixture.Connection, NullLogger<ListeningEventRepository>.Instance, TimeProvider.System);

    private static Task InsertEventAsync(
        SqliteDatabaseFixture fixture,
        long trackId,
        long? artistId,
        long? albumId,
        long? genreId,
        DateTime playedAt,
        bool wasSkipped = false,
        long durationPlayed = 180,
        long durationTotal = 180)
    {
        return fixture.Connection.ExecuteAsync(@"
            INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal)
            VALUES (@trackId, @artistId, @albumId, @genreId, @playedAt, @wasSkipped, @durationPlayed, @durationTotal)",
            new { trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal });
    }

    private static Task SeedExtraTrackAsync(SqliteDatabaseFixture fixture, long trackId, long albumId, long artistId)
    {
        DateTime now = DateTime.UtcNow;
        return fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, @title, 180, 1000, 128, @file, @now, 0, 0, 0, 0, @now, @albumId, @artistId, 1)",
            new { id = trackId, title = $"track_{trackId}", file = $"/f{trackId}", now, albumId, artistId });
    }

    private static Task SeedExtraArtistAsync(SqliteDatabaseFixture fixture, long artistId, string name)
    {
        DateTime now = DateTime.UtcNow;
        return fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, @name, 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = artistId, name, now });
    }

    [Fact(DisplayName = "GetTableName should return listeningevents")]
    public void GetTableName_ShouldReturnListeningevents()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);

        // Act
        string name = repo.GetTableName();

        // Assert
        Assert.Equal("listeningevents", name);
    }

    [Fact(DisplayName = "GetInsightsAsync should return zeroed stats when no events exist")]
    public async Task GetInsightsAsync_ShouldReturnZeroedStats_WhenNoEvents()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(0, insights.SessionCount);
        Assert.Equal(0, insights.PreviousSessionCount);
        Assert.Equal(0, insights.PlayDuration);
        Assert.Equal(0, insights.TracksPlayed);
        Assert.Equal(0, insights.ArtistsPlayed);
        Assert.Equal(0, insights.AlbumsPlayed);
        Assert.Equal(0, insights.GenresPlayed);
        Assert.Equal(0d, insights.SkipRate);
        Assert.Equal(0d, insights.ReplayRate);
        Assert.Equal(0, insights.LongSessionCount);
        Assert.Equal(-1, insights.GlobalPeakHour);
        Assert.Empty(insights.TopTracks);
        Assert.Empty(insights.TopAlbums);
        Assert.Empty(insights.TopGenres);
    }

    [Fact(DisplayName = "GetInsightsAsync should aggregate current month tracks artists albums and genres")]
    public async Task GetInsightsAsync_ShouldAggregateCurrentMonthCounts()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime apr5 = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, trackId: 1, artistId: 1, albumId: 1, genreId: 1, playedAt: apr5);
        await InsertEventAsync(fixture, trackId: 2, artistId: 1, albumId: 1, genreId: 2, playedAt: apr5.AddMinutes(5));
        await InsertEventAsync(fixture, trackId: 3, artistId: 2, albumId: 2, genreId: 2, playedAt: apr5.AddMinutes(10));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(3, insights.TracksPlayed);
        Assert.Equal(2, insights.ArtistsPlayed);
        Assert.Equal(2, insights.AlbumsPlayed);
        Assert.Equal(2, insights.GenresPlayed);
        Assert.Equal(540, insights.PlayDuration);
        Assert.Equal(1, insights.SessionCount);
    }

    [Fact(DisplayName = "GetInsightsAsync should populate previous month counts separately from current month")]
    public async Task GetInsightsAsync_ShouldPopulatePreviousMonthCounts()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime mar15 = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        DateTime apr10 = new(2026, 4, 10, 11, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, mar15);
        await InsertEventAsync(fixture, 2, 1, 1, 2, mar15.AddMinutes(5));
        await InsertEventAsync(fixture, 3, 2, 2, 1, apr10);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(1, insights.SessionCount);
        Assert.Equal(1, insights.PreviousSessionCount);
        Assert.Equal(1, insights.TracksPlayed);
        Assert.Equal(2, insights.PreviousTracksPlayed);
        Assert.Equal(1, insights.ArtistsPlayed);
        Assert.Equal(1, insights.PreviousArtistsPlayed);
        Assert.Equal(1, insights.AlbumsPlayed);
        Assert.Equal(1, insights.PreviousAlbumsPlayed);
        Assert.Equal(1, insights.GenresPlayed);
        Assert.Equal(2, insights.PreviousGenresPlayed);
    }

    [Fact(DisplayName = "GetInsightsAsync should ignore events outside the two-month window")]
    public async Task GetInsightsAsync_ShouldIgnoreEventsOutsideWindow()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        await InsertEventAsync(fixture, 1, 1, 1, 1, new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc));
        await InsertEventAsync(fixture, 1, 1, 1, 1, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(0, insights.SessionCount);
        Assert.Equal(0, insights.PreviousSessionCount);
        Assert.Equal(0, insights.TracksPlayed);
        Assert.Equal(0, insights.PreviousTracksPlayed);
    }

    [Fact(DisplayName = "GetInsightsAsync should detect a long session when at least five events fit within thirty minute gaps")]
    public async Task GetInsightsAsync_ShouldDetectLongSession()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
        {
            await InsertEventAsync(fixture, trackId: 1, artistId: 1, albumId: 1, genreId: 1, playedAt: start.AddMinutes(i * 5));
        }

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(1, insights.SessionCount);
        Assert.Equal(1, insights.LongSessionCount);
    }

    [Fact(DisplayName = "GetInsightsAsync should start a new session when the gap exceeds thirty minutes")]
    public async Task GetInsightsAsync_ShouldStartNewSession_WhenGapExceedsThirtyMinutes()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 9, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start);
        await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(31));
        await InsertEventAsync(fixture, 3, 2, 2, 1, start.AddHours(2));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(3, insights.SessionCount);
        Assert.Equal(0, insights.LongSessionCount);
    }

    [Fact(DisplayName = "GetInsightsAsync should exclude skipped events from PlayDuration")]
    public async Task GetInsightsAsync_ShouldExcludeSkippedEventsFromPlayDuration()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start, wasSkipped: false, durationPlayed: 200);
        await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(5), wasSkipped: true, durationPlayed: 30);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(200, insights.PlayDuration);
    }

    [Fact(DisplayName = "GetInsightsAsync should compute SkipRate as percentage of skipped events")]
    public async Task GetInsightsAsync_ShouldComputeSkipRate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 8; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5), wasSkipped: false);
        for (int i = 0; i < 2; i++)
            await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes((8 + i) * 5), wasSkipped: true);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(20d, insights.SkipRate);
    }

    [Fact(DisplayName = "GetInsightsAsync should count a replay when same track appears within two hours")]
    public async Task GetInsightsAsync_ShouldCountReplay_WhenSameTrackWithinTwoHours()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start);
        await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(10));
        await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(20));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(100d / 3, insights.ReplayRate, 3);
    }

    [Fact(DisplayName = "GetInsightsAsync should not count a replay when same track is more than two hours apart")]
    public async Task GetInsightsAsync_ShouldNotCountReplay_WhenGapExceedsTwoHours()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddHours(3));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(0d, insights.ReplayRate);
    }

    [Fact(DisplayName = "GetInsightsAsync should compute global peak hour as the most active hour")]
    public async Task GetInsightsAsync_ShouldComputeGlobalPeakHour()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime apr5 = new(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, apr5.AddHours(8));
        await InsertEventAsync(fixture, 2, 1, 1, 2, apr5.AddHours(15));
        await InsertEventAsync(fixture, 3, 2, 2, 1, apr5.AddHours(15).AddMinutes(31));
        await InsertEventAsync(fixture, 1, 1, 1, 1, apr5.AddHours(15).AddHours(3));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(15, insights.GlobalPeakHour);
    }

    [Fact(DisplayName = "GetInsightsAsync should return TopTracks ordered by play count limited to five and excluding skipped")]
    public async Task GetInsightsAsync_TopTracks_OrderedByPlayCount_ExcludingSkipped()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 4; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5));
        for (int i = 0; i < 2; i++)
            await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes((4 + i) * 5));
        await InsertEventAsync(fixture, 3, 2, 2, 1, start.AddMinutes(40), wasSkipped: true);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(2, insights.TopTracks.Count);
        Assert.Equal("t1", insights.TopTracks[0].Title);
        Assert.Equal(4, insights.TopTracks[0].PlayCount);
        Assert.Equal("t2", insights.TopTracks[1].Title);
        Assert.Equal(2, insights.TopTracks[1].PlayCount);
    }

    [Fact(DisplayName = "GetInsightsAsync should return TopAlbums ignoring events without an album id")]
    public async Task GetInsightsAsync_TopAlbums_IgnoresEventsWithoutAlbum()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 1, 1, 1, 1, start);
        await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(5));
        await InsertEventAsync(fixture, 3, 2, null, 1, start.AddMinutes(10));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Single(insights.TopAlbums);
        Assert.Equal("The First Album", insights.TopAlbums[0].Title);
        Assert.Equal(2, insights.TopAlbums[0].PlayCount);
        Assert.Equal(1, insights.AlbumsPlayed);
    }

    [Fact(DisplayName = "GetInsightsAsync should return TopGenres limited to three with PlayPercentage")]
    public async Task GetInsightsAsync_TopGenres_LimitedToThreeWithPercentage()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 3; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5), durationPlayed: 100);
        await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(20), durationPlayed: 100);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(2, insights.TopGenres.Count);
        Assert.Equal(1, insights.TopGenres[0].Rank);
        Assert.Equal("Rock", insights.TopGenres[0].GenreName);
        Assert.Equal(75d, insights.TopGenres[0].PlayPercentage);
        Assert.Equal(2, insights.TopGenres[1].Rank);
        Assert.Equal("Jazz", insights.TopGenres[1].GenreName);
    }

    [Fact(DisplayName = "GetInsightsAsync should always return one heatmap cell per day-hour pair")]
    public async Task GetInsightsAsync_HeatmapShouldHave168Cells()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        await InsertEventAsync(fixture, 1, 1, 1, 1, new DateTime(2026, 4, 6, 14, 30, 0, DateTimeKind.Utc));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(168, insights.HeatmapCells.Count);
        HeatmapCellDto monday14 = insights.HeatmapCells.Single(c => c.DayOfWeek == 0 && c.Hour == 14);
        Assert.Equal(1, monday14.Count);
        Assert.True(insights.HeatmapCells.Where(c => !(c.DayOfWeek == 0 && c.Hour == 14)).All(c => c.Count == 0));
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct skip badge based on skip rate")]
    [InlineData(0, 10, Badge.SmoothListener)]
    [InlineData(1, 15, Badge.LowSkip)]
    [InlineData(4, 10, Badge.Zapper)]
    [InlineData(7, 10, Badge.HyperZapper)]
    public async Task GetInsightsAsync_SkipBadge_BasedOnRate(int skippedCount, int totalCount, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < totalCount - skippedCount; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5), wasSkipped: false);
        for (int i = 0; i < skippedCount; i++)
            await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddHours(3).AddMinutes(i * 5), wasSkipped: true);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct replay badge based on replay rate")]
    [InlineData(true, Badge.Obsessed)]
    [InlineData(false, Badge.FreshSeeker)]
    public async Task GetInsightsAsync_ReplayBadge_BasedOnRate(bool replayHeavy, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        if (replayHeavy)
        {
            for (int i = 0; i < 6; i++)
                await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5));
        }
        else
        {
            await InsertEventAsync(fixture, 1, 1, 1, 1, start);
            await InsertEventAsync(fixture, 2, 1, 1, 2, start.AddMinutes(5));
            await InsertEventAsync(fixture, 3, 2, 2, 1, start.AddMinutes(10));
        }

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct diversity badge based on artist count")]
    [InlineData(2, Badge.UltraFocus)]
    [InlineData(7, Badge.RestrictedCircle)]
    [InlineData(25, Badge.Curious)]
    [InlineData(35, Badge.Explorer)]
    public async Task GetInsightsAsync_DiversityBadge_BasedOnArtistCount(int artistCount, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < artistCount; i++)
        {
            long artistId = 100 + i;
            long trackId = 100 + i;
            await SeedExtraArtistAsync(fixture, artistId, $"Artist_{i}");
            await SeedExtraTrackAsync(fixture, trackId, albumId: 1, artistId: artistId);
            await InsertEventAsync(fixture, trackId, artistId, 1, 1, start.AddHours(i));
        }

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct peak hour badge based on global peak hour")]
    [InlineData(2, Badge.NightOwl)]
    [InlineData(8, Badge.EarlyBird)]
    [InlineData(18, Badge.Afterwork)]
    [InlineData(22, Badge.Nocturne)]
    public async Task GetInsightsAsync_PeakHourBadge_BasedOnHour(int peakHour, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime apr5 = new(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 3; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, apr5.AddHours(peakHour).AddMinutes(i * 5));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(peakHour, insights.GlobalPeakHour);
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct long-session badge based on long session count")]
    [InlineData(1, Badge.ShortSessions)]
    [InlineData(12, Badge.LongPlayer)]
    [InlineData(20, Badge.DeepListener)]
    public async Task GetInsightsAsync_LongSessionBadge_BasedOnCount(int longSessionCount, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime baseDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int s = 0; s < longSessionCount; s++)
        {
            DateTime sessionStart = baseDate.AddHours(s * 2);
            for (int i = 0; i < 5; i++)
                await InsertEventAsync(fixture, 1, 1, 1, 1, sessionStart.AddMinutes(i * 5));
        }

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(longSessionCount, insights.LongSessionCount);
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Theory(DisplayName = "GetInsightsAsync should return correct fidelity badge based on track replay fidelity rate")]
    [InlineData(40, Badge.UltraLoyal)]
    [InlineData(25, Badge.Loyal)]
    [InlineData(0, Badge.Eclectic)]
    public async Task GetInsightsAsync_FidelityBadge_BasedOnFidelityRate(int loyalPercentage, Badge expected)
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        const int totalTracks = 20;
        int loyalTracks = totalTracks * loyalPercentage / 100;

        for (int t = 0; t < loyalTracks; t++)
        {
            long trackId = 200 + t;
            await SeedExtraTrackAsync(fixture, trackId, 1, 1);
            for (int i = 0; i < 4; i++)
                await InsertEventAsync(fixture, trackId, 1, 1, 1, start.AddDays(t).AddHours(i * 3));
        }
        for (int t = loyalTracks; t < totalTracks; t++)
        {
            long trackId = 200 + t;
            await SeedExtraTrackAsync(fixture, trackId, 1, 1);
            await InsertEventAsync(fixture, trackId, 1, 1, 1, start.AddDays(t));
        }

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Contains(insights.Badges, b => b.Id == expected);
    }

    [Fact(DisplayName = "GetInsightsAsync should return ChannelSurfer profile when skip rate dominates")]
    public async Task GetInsightsAsync_Profile_ShouldBeChannelSurfer_OnHighSkipRate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime start = new(2026, 4, 5, 14, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 10; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, start.AddMinutes(i * 5), wasSkipped: i >= 2);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(ListeningProfile.ChannelSurfer, insights.ListeningProfile);
    }

    [Fact(DisplayName = "GetInsightsAsync should return NightOwl profile when peak hour is late at night")]
    public async Task GetInsightsAsync_Profile_ShouldBeNightOwl_OnLatePeakHour()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime apr5 = new(2026, 4, 5, 23, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 3; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, apr5.AddMinutes(i * 5));

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(ListeningProfile.NightOwl, insights.ListeningProfile);
    }

    [Fact(DisplayName = "GetInsightsAsync SessionStats should compute MaxDuration MostIntenseSession and MostActiveDay")]
    public async Task GetInsightsAsync_SessionStats_ShouldComputeAllMetrics()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ListeningEventRepository repo = CreateRepository(fixture);
        DateTime monday22 = new(2026, 4, 6, 22, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 6; i++)
            await InsertEventAsync(fixture, 1, 1, 1, 1, monday22.AddMinutes(i * 5), durationPlayed: 200);
        DateTime tuesday22 = new(2026, 4, 7, 22, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
            await InsertEventAsync(fixture, 2, 1, 1, 2, tuesday22.AddMinutes(i * 5), durationPlayed: 100);
        DateTime wednesday10 = new(2026, 4, 8, 10, 0, 0, DateTimeKind.Utc);
        await InsertEventAsync(fixture, 2, 1, 1, 2, wednesday10, durationPlayed: 100);

        // Act
        InsightsDto insights = await repo.GetInsightsAsync(CurrentMonth);

        // Assert
        Assert.Equal(3, insights.SessionCount);
        Assert.Equal(1200, insights.SessionStats.MaxDurationSeconds);
        Assert.Equal(4d, insights.SessionStats.AverageTracksPerSession);
        Assert.Equal(200d / 3, insights.SessionStats.NocturnalSessionPercentage, 3);
        Assert.Equal(22, insights.SessionStats.MostCommonStartHour);
        Assert.NotNull(insights.SessionStats.MostIntenseSession);
        Assert.Equal(6, insights.SessionStats.MostIntenseSession!.TrackCount);
        Assert.Equal(1200, insights.SessionStats.MostIntenseSession.DurationSeconds);
        Assert.Equal("Rock", insights.SessionStats.MostIntenseSession.DominantGenre);
        Assert.Equal(0, insights.SessionStats.MostActiveDayOfWeek);
        Assert.Equal(1, insights.SessionStats.MostActiveDaySessionCount);
    }
}
