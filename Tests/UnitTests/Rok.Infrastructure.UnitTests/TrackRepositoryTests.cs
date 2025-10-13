using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class TrackRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private TrackRepository CreateRepository()
    {
        return new TrackRepository(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingTracks()
    {
        // Arrange
        TrackRepository repo = CreateRepository();

        // Act
        var result = (await repo.SearchAsync("t1")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("t1", result[0].Title);
    }

    [Fact]
    public async Task GetByAlbumIdAsync_ReturnsTracksForAlbum()
    {
        // Arrange
        TrackRepository repo = CreateRepository();

        // Act
        var result = (await repo.GetByAlbumIdAsync(1)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == 1);
        Assert.Contains(result, t => t.Id == 2);
    }

    [Fact]
    public async Task GetByArtistIdAsync_ReturnsTracksOrdered()
    {
        // Arrange
        TrackRepository repo = CreateRepository();

        // Act
        var result = (await repo.GetByArtistIdAsync(1)).ToList();

        // Assert: same album -> ordered by trackNumber asc
        Assert.True(result.Count >= 2);
        Assert.Equal(1, result[0].TrackNumber);
        Assert.Equal(2, result[1].TrackNumber);
    }

    [Fact]
    public async Task GetByAlbumIds_WithLimit_ReturnsLimitedRandomSelection()
    {
        // Arrange
        TrackRepository repo = CreateRepository();
        var albumIds = new long[] { 1, 2 };

        // Act
        var result = (await repo.GetByAlbumIdAsync(albumIds, limit: 2)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateScoreAsync_UpdatesScore()
    {
        // Arrange
        TrackRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateScoreAsync(1, 7);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(1);
        Assert.NotNull(track);
        Assert.Equal(7, track!.Score);
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        TrackRepository repo = CreateRepository();
        TrackEntity? before = await repo.GetByIdAsync(1);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(1);

        // Assert
        Assert.True(ok);
        TrackEntity? after = await repo.GetByIdAsync(1);
        Assert.NotNull(after);
        Assert.Equal(beforeCount + 1, after!.ListenCount);
        Assert.NotNull(after.LastListen);
        Assert.True((DateTime.UtcNow - after.LastListen.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateSkipCountAsync_IncrementsSkipCountAndSetsLastSkip()
    {
        // Arrange
        TrackRepository repo = CreateRepository();
        TrackEntity? before = await repo.GetByIdAsync(1);
        int beforeSkip = before?.SkipCount ?? 0;

        // Act
        bool ok = await repo.UpdateSkipCountAsync(1);

        // Assert
        Assert.True(ok);
        TrackEntity? after = await repo.GetByIdAsync(1);
        Assert.NotNull(after);
        Assert.Equal(beforeSkip + 1, after!.SkipCount);
        Assert.NotNull(after.LastSkip);
        Assert.True((DateTime.UtcNow - after.LastSkip.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateFileDateAsync_UpdatesFileDate()
    {
        // Arrange
        TrackRepository repo = CreateRepository();
        var newDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        bool ok = await repo.UpdateFileDateAsync(1, newDate);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(1);
        Assert.NotNull(track);
        Assert.Equal(newDate, track!.FileDate);
    }

    [Fact]
    public async Task UpdateGetLyricsLastAttemptAsync_SetsTimestamp()
    {
        // Arrange
        TrackRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateGetLyricsLastAttemptAsync(1);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(1);
        Assert.NotNull(track);
        Assert.NotNull(track.GetLyricsLastAttempt);
        Assert.True((DateTime.UtcNow - track.GetLyricsLastAttempt.Value).TotalSeconds < 10);
    }
}