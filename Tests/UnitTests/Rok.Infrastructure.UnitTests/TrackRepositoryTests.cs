using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class TrackRepositoryTests
{
    private static SqliteDatabaseFixture CreateFixture()
    {
        return new SqliteDatabaseFixture();
    }

    private static TrackRepository CreateRepository(SqliteDatabaseFixture fixture)
    {
        return new TrackRepository(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithNullString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync(null!)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("   ")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithMatchingTitle_ReturnsMatchingTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("t1")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("t1", result[0].Title);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ReturnsMatchingTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("T1")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("t1", result[0].Title);
    }

    [Fact]
    public async Task SearchAsync_WithPartialMatch_ReturnsMatchingTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("t")).ToList();

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SearchAsync_WithSpecialCharacters_ReturnsNoResults()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("%_[]")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByPlaylistIdAsync_ReturnsPlaylistTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testPlaylistId = 8001;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Playlists(id, name, type, creatDate) 
            VALUES (@id, 'Test Playlist', 1, @now)",
            new { id = testPlaylistId, now });

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate)
            VALUES (8001, @playlistId, 1, 0, 0, @now)",
            new { playlistId = testPlaylistId, now });

        // Act
        List<TrackEntity> result = (await repo.GetByPlaylistIdAsync(testPlaylistId)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains(result, t => t.Id == 1);
    }

    [Fact]
    public async Task GetByPlaylistIdAsync_WithInvalidPlaylistId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByPlaylistIdAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByPlaylistIdAsync(-1));
    }

    [Fact]
    public async Task GetByGenreIdAsync_ReturnsGenreTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber, genreId)
            VALUES (8002, 'Track With Genre', 180, 1000, 128, '/test8002', @now, 0, 0, 0, 0, @now, 1, 1, 1, 1)",
            new { now });

        // Act
        List<TrackEntity> result = (await repo.GetByGenreIdAsync(1)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains(result, t => t.Id == 8002);
    }

    [Fact]
    public async Task GetByArtistIdAsync_ReturnsArtistTracksOrdered()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 8100;
        long testAlbumId = 8101;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@artistId, 'Test Artist Order', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { artistId = testArtistId, now });

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, year)
            VALUES (@albumId, 'Test Album Order', 0, 0, 0, 2, 600, 0, 0, @now, @artistId, 1, 2020)",
            new { albumId = testAlbumId, artistId = testArtistId, now });

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES 
            (8102, 'Track Order 1', 180, 1000, 128, '/test8102', @now, 0, 0, 0, 0, @now, @albumId, @artistId, 2),
            (8103, 'Track Order 2', 200, 1200, 128, '/test8103', @now, 0, 0, 0, 0, @now, @albumId, @artistId, 1)",
            new { albumId = testAlbumId, artistId = testArtistId, now });

        // Act
        List<TrackEntity> result = (await repo.GetByArtistIdAsync(testArtistId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].TrackNumber);
        Assert.Equal(2, result[1].TrackNumber);
    }

    [Fact]
    public async Task GetByArtistIdAsync_WithMultipleIds_ReturnsRandomLimitedSelection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        long[] artistIds = new long[] { 1, 2 };

        // Act
        List<TrackEntity> result = (await repo.GetByArtistIdAsync(artistIds, limit: 2)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByArtistIdAsync_WithEmptyIds_ReturnsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        long[] artistIds = Array.Empty<long>();

        // Act
        List<TrackEntity> result = (await repo.GetByArtistIdAsync(artistIds, limit: 10)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByArtistIdAsync_WithNullIds_ReturnsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.GetByArtistIdAsync(null!, limit: 10)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByAlbumIdAsync_ReturnsAlbumTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 8110;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@albumId, 'Test Album Tracks', 0, 0, 0, 2, 600, 0, 0, @now, 1, 1)",
            new { albumId = testAlbumId, now });

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES 
            (8111, 'Album Track 1', 180, 1000, 128, '/test8111', @now, 0, 0, 0, 0, @now, @albumId, 1, 1),
            (8112, 'Album Track 2', 200, 1200, 128, '/test8112', @now, 0, 0, 0, 0, @now, @albumId, 1, 2)",
            new { albumId = testAlbumId, now });

        // Act
        List<TrackEntity> result = (await repo.GetByAlbumIdAsync(testAlbumId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == 8111);
        Assert.Contains(result, t => t.Id == 8112);
    }

    [Fact]
    public async Task GetByAlbumIdAsync_WithInvalidAlbumId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByAlbumIdAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByAlbumIdAsync(-1));
    }

    [Fact]
    public async Task GetByAlbumIdAsync_WithMultipleIds_ReturnsRandomLimitedSelection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        long[] albumIds = new long[] { 1, 2 };

        // Act
        List<TrackEntity> result = (await repo.GetByAlbumIdAsync(albumIds, limit: 2)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByAlbumIdAsync_WithEmptyIds_ReturnsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        long[] albumIds = Array.Empty<long>();

        // Act
        List<TrackEntity> result = (await repo.GetByAlbumIdAsync(albumIds, limit: 10)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByAlbumIdAsync_WithNullIds_ReturnsEmpty()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.GetByAlbumIdAsync(null!, limit: 10)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateScoreAsync_UpdatesScore()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8003;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Test Track Score', 180, 1000, 128, '/test8003', @now, 0, 0, 0, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        // Act
        bool ok = await repo.UpdateScoreAsync(testTrackId, 8);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(track);
        Assert.Equal(8, track!.Score);
    }

    [Fact]
    public async Task UpdateScoreAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateScoreAsync(0, 5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateScoreAsync(-1, 5));
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8004;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Test Track Listen', 180, 1000, 128, '/test8004', @now, 0, 0, 3, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        TrackEntity? before = await repo.GetByIdAsync(testTrackId);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(testTrackId);

        // Assert
        Assert.True(ok);
        TrackEntity? after = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(after);
        Assert.Equal(beforeCount + 1, after!.ListenCount);
        Assert.NotNull(after.LastListen);
        Assert.True((DateTime.UtcNow - after.LastListen.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateLastListenAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(-1));
    }

    [Fact]
    public async Task UpdateSkipCountAsync_IncrementsSkipCountAndSetsLastSkip()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8005;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Test Track Skip', 180, 1000, 128, '/test8005', @now, 0, 0, 0, 2, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        TrackEntity? before = await repo.GetByIdAsync(testTrackId);
        int beforeCount = before?.SkipCount ?? 0;

        // Act
        bool ok = await repo.UpdateSkipCountAsync(testTrackId);

        // Assert
        Assert.True(ok);
        TrackEntity? after = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(after);
        Assert.Equal(beforeCount + 1, after!.SkipCount);
        Assert.NotNull(after.LastSkip);
        Assert.True((DateTime.UtcNow - after.LastSkip.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateSkipCountAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateSkipCountAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateSkipCountAsync(-1));
    }

    [Fact]
    public async Task UpdateFileDateAsync_UpdatesFileDate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8006;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Test Track FileDate', 180, 1000, 128, '/test8006', @now, 0, 0, 0, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        DateTime newFileDate = new(2023, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        bool ok = await repo.UpdateFileDateAsync(testTrackId, newFileDate);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(track);
        Assert.Equal(newFileDate, track!.FileDate);
    }

    [Fact]
    public async Task UpdateFileDateAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime fileDate = DateTime.UtcNow;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFileDateAsync(0, fileDate));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFileDateAsync(-1, fileDate));
    }

    [Fact]
    public async Task UpdateGetLyricsLastAttemptAsync_UpdatesLastAttemptDate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8007;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Test Track Lyrics', 180, 1000, 128, '/test8007', @now, 0, 0, 0, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        DateTime beforeNow = DateTime.UtcNow;

        // Act
        bool ok = await repo.UpdateGetLyricsLastAttemptAsync(testTrackId);

        // Assert
        Assert.True(ok);
        TrackEntity? track = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(track);
        Assert.NotNull(track!.GetLyricsLastAttempt);
        Assert.True(track.GetLyricsLastAttempt >= beforeNow);
        Assert.True((DateTime.UtcNow - track.GetLyricsLastAttempt.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateGetLyricsLastAttemptAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetLyricsLastAttemptAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetLyricsLastAttemptAsync(-1));
    }

    [Fact]
    public async Task AddAsync_AddsNewTrack_ReturnsId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        TrackEntity newTrack = new()
        {
            Title = "Brand New Track",
            Duration = 240,
            Size = 5000,
            Bitrate = 320,
            MusicFile = "/music/new-track-8010.mp3",
            FileDate = DateTime.UtcNow,
            IsLive = false,
            Score = 0,
            ListenCount = 0,
            SkipCount = 0,
            CreatDate = DateTime.UtcNow,
            AlbumId = 1,
            ArtistId = 1,
            TrackNumber = 10
        };

        // Act
        long id = await repo.AddAsync(newTrack);

        // Assert
        Assert.True(id > 0);
        TrackEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Brand New Track", fetched!.Title);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingTrack_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8020;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Track To Update', 180, 1000, 128, '/test8020', @now, 0, 0, 0, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        TrackEntity? track = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(track);
        track!.Title = "Updated Track Title";
        track.Score = 9;

        // Act
        bool result = await repo.UpdateAsync(track);

        // Assert
        Assert.True(result);
        TrackEntity? updated = await repo.GetByIdAsync(testTrackId);
        Assert.Equal("Updated Track Title", updated?.Title);
        Assert.Equal(9, updated?.Score);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingTrack_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testTrackId = 8021;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES (@id, 'Track To Delete', 180, 1000, 128, '/test8021', @now, 0, 0, 0, 0, @now, 1, 1, 1)",
            new { id = testTrackId, now });

        TrackEntity? track = await repo.GetByIdAsync(testTrackId);
        Assert.NotNull(track);

        // Act
        bool result = await repo.DeleteAsync(track!);

        // Assert
        Assert.True(result);
        TrackEntity? deleted = await repo.GetByIdAsync(testTrackId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.Title == "t1");
        Assert.Contains(result, t => t.Title == "t2");
        Assert.Contains(result, t => t.Title == "t3");
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        int count = await repo.CountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? result = await repo.GetByIdAsync(999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingTitle_ReturnsTrack()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? result = await repo.GetByNameAsync("t1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("t1", result!.Title);
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistentTitle_ReturnsNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? result = await repo.GetByNameAsync("Non Existent Track");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithBackgroundConnection_ReturnsTrack()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        TrackEntity? result = await repo.GetByIdAsync(1, RepositoryConnectionKind.Background);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("t1", result!.Title);
    }

    [Fact]
    public async Task SearchAsync_WithBackgroundConnection_ReturnsMatchingTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        TrackRepository repo = CreateRepository(fixture);

        // Act
        List<TrackEntity> result = (await repo.SearchAsync("t1", RepositoryConnectionKind.Background)).ToList();

        // Assert
        Assert.Single(result);
    }
}