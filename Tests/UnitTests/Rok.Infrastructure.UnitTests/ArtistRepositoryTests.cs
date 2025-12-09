using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using Rok.Domain.Interfaces.Entities;
using Rok.Infrastructure.Repositories;
using Rok.Shared;

namespace Rok.Infrastructure.UnitTests;

public class ArtistRepositoryTests
{
    private static SqliteDatabaseFixture CreateFixture()
    {
        return new SqliteDatabaseFixture();
    }

    private static ArtistRepository CreateRepository(SqliteDatabaseFixture fixture)
    {
        return new ArtistRepository(fixture.Connection, fixture.Connection, NullLogger<ArtistRepository>.Instance);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithNullString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync(null!)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("   ")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithMatchingName_ReturnsMatchingArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("Artist A")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Artist A", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ReturnsMatchingArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("ARTIST A")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Artist A", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_WithPartialMatch_ReturnsMatchingArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("Artist")).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "Artist A");
        Assert.Contains(result, a => a.Name == "Artist B");
    }

    [Fact]
    public async Task SearchAsync_WithSpecialCharacters_ReturnsNoResults()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("%_[]")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByGenreIdAsync_ReturnsGenreArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate, genreId)
            VALUES (7001, 'Artist With Genre', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now, 1)",
            new { now });

        // Act
        List<IArtistEntity> result = (await repo.GetByGenreIdAsync(1)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains(result, a => a.Id == 7001);
    }

    [Fact]
    public async Task GetByGenreIdAsync_WithInvalidGenreId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByGenreIdAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByGenreIdAsync(-1));
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavoriteToTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7002;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Favorite', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        // Act
        bool ok = await repo.UpdateFavoriteAsync(testArtistId, true);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.True(artist!.IsFavorite);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavoriteToFalse()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7003;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist UnFavorite', 0, 0, 0, 0, 0, 0, 0, 1, 0, @now)",
            new { id = testArtistId, now });

        // Act
        bool ok = await repo.UpdateFavoriteAsync(testArtistId, false);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.False(artist!.IsFavorite);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFavoriteAsync(0, true));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFavoriteAsync(-1, true));
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7004;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Listen', 0, 0, 0, 0, 0, 0, 0, 0, 5, @now)",
            new { id = testArtistId, now });

        ArtistEntity? before = await repo.GetByIdAsync(testArtistId);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(testArtistId);

        // Assert
        Assert.True(ok);
        ArtistEntity? after = await repo.GetByIdAsync(testArtistId);
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
        ArtistRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(-1));
    }

    [Fact]
    public async Task UpdateStatisticsAsync_UpdatesAllStatistics()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7005;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Stats', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        // Act
        bool ok = await repo.UpdateStatisticsAsync(
            testArtistId,
            trackCount: 50,
            totalDurationSeconds: 18000,
            albumCount: 5,
            bestOfCount: 1,
            liveCount: 2,
            compilationCount: 1,
            yearMini: 2000,
            yearMaxi: 2023);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Equal(50, artist!.TrackCount);
        Assert.Equal(18000, artist.TotalDurationSeconds);
        Assert.Equal(5, artist.AlbumCount);
        Assert.Equal(1, artist.BestofCount);
        Assert.Equal(2, artist.LiveCount);
        Assert.Equal(1, artist.CompilationCount);
        Assert.Equal(2000, artist.YearMini);
        Assert.Equal(2023, artist.YearMaxi);
    }

    [Fact]
    public async Task UpdateStatisticsAsync_WithNullYears_UpdatesCorrectly()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7006;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate, yearMini, yearMaxi)
            VALUES (@id, 'Test Artist Stats Null', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now, 2000, 2020)",
            new { id = testArtistId, now });

        // Act
        bool ok = await repo.UpdateStatisticsAsync(
            testArtistId,
            trackCount: 10,
            totalDurationSeconds: 3600,
            albumCount: 2,
            bestOfCount: 0,
            liveCount: 0,
            compilationCount: 0,
            yearMini: null,
            yearMaxi: null);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Null(artist!.YearMini);
        Assert.Null(artist.YearMaxi);
    }

    [Fact]
    public async Task UpdateStatisticsAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repo.UpdateStatisticsAsync(0, 10, 3600, 2, 0, 0, 0, null, null));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repo.UpdateStatisticsAsync(-1, 10, 3600, 2, 0, 0, 0, null, null));
    }

    [Fact]
    public async Task UpdateGetMetaDataLastAttemptAsync_UpdatesLastAttemptDate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7007;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Metadata', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        DateTime beforeNow = DateTime.UtcNow;

        // Act
        bool ok = await repo.UpdateGetMetaDataLastAttemptAsync(testArtistId);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.NotNull(artist!.GetMetaDataLastAttempt);
        Assert.True(artist.GetMetaDataLastAttempt >= beforeNow);
        Assert.True((DateTime.UtcNow - artist.GetMetaDataLastAttempt.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateGetMetaDataLastAttemptAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetMetaDataLastAttemptAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetMetaDataLastAttemptAsync(-1));
    }

    [Fact]
    public async Task DeleteOrphansAsync_RemovesArtistsWithoutTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long orphanArtistId = 9100;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Orphan Artist', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = orphanArtistId, now });

        // Act
        int deleted = await repo.DeleteOrphansAsync();

        // Assert
        Assert.Equal(1, deleted);
        ArtistEntity? orphan = await repo.GetByIdAsync(orphanArtistId);
        Assert.Null(orphan);
    }

    [Fact]
    public async Task DeleteOrphansAsync_WithNoOrphans_ReturnsZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        int deleted = await repo.DeleteOrphansAsync();

        // Assert
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task PatchAsync_UpdatesBiographyField()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7008;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Patch', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        UpdateArtistEntity patchEntity = new()
        {
            Id = testArtistId,
            Biography = new PatchField<string>("New biography text")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Equal("New biography text", artist!.Biography);
    }

    [Fact]
    public async Task PatchAsync_UpdatesMultipleFields()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7009;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Multi Patch', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        UpdateArtistEntity patchEntity = new()
        {
            Id = testArtistId,
            Biography = new PatchField<string>("Artist biography"),
            Style = new PatchField<string>("Rock"),
            Mood = new PatchField<string>("Energetic"),
            Gender = new PatchField<string>("Male"),
            FormedYear = new PatchField<int>(1995),
            Disbanded = new PatchField<bool>(false)
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Equal("Artist biography", artist!.Biography);
        Assert.Equal("Rock", artist.Style);
        Assert.Equal("Energetic", artist.Mood);
        Assert.Equal("Male", artist.Gender);
        Assert.Equal(1995, artist.FormedYear);
        Assert.False(artist.Disbanded);
    }

    [Fact]
    public async Task PatchAsync_WithNullValues_SetsFieldsToNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7030;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate, biography, mood)
            VALUES (@id, 'Test Artist Null Patch', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now, 'Original Bio', 'Original Mood')",
            new { id = testArtistId, now });

        UpdateArtistEntity patchEntity = new()
        {
            Id = testArtistId,
            Biography = new PatchField<string>(null),
            Mood = new PatchField<string>(null)
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Null(artist!.Biography);
        Assert.Null(artist.Mood);
    }

    [Fact]
    public async Task PatchAsync_WithAllFields_UpdatesAllFields()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7031;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Test Artist Full Patch', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        UpdateArtistEntity patchEntity = new()
        {
            Id = testArtistId,
            Biography = new PatchField<string>("Complete biography"),
            WikipediaUrl = new PatchField<string>("https://en.wikipedia.org/wiki/Artist"),
            OfficialSiteUrl = new PatchField<string>("https://artist.com"),
            FacebookUrl = new PatchField<string>("https://facebook.com/artist"),
            TwitterUrl = new PatchField<string>("https://twitter.com/artist"),
            NovaUid = new PatchField<string>("nova-12345"),
            MusicBrainzID = new PatchField<string>("mb-67890"),
            FormedYear = new PatchField<int>(1990),
            BornYear = new PatchField<int>(1970),
            DiedYear = new PatchField<int>(2020),
            Disbanded = new PatchField<bool>(true),
            Style = new PatchField<string>("Alternative Rock"),
            Gender = new PatchField<string>("Male"),
            Mood = new PatchField<string>("Melancholic")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        Assert.Equal("Complete biography", artist!.Biography);
        Assert.Equal("https://en.wikipedia.org/wiki/Artist", artist.WikipediaUrl);
        Assert.Equal("https://artist.com", artist.OfficialSiteUrl);
        Assert.Equal("mb-67890", artist.MusicBrainzID);
        Assert.Equal(1990, artist.FormedYear);
        Assert.True(artist.Disbanded);
    }

    [Fact]
    public async Task PatchAsync_WithNonExistentArtist_ReturnsFalse()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        UpdateArtistEntity patchEntity = new()
        {
            Id = 999999,
            Biography = new PatchField<string>("Test")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public async Task PatchAsync_WithUnsetFields_DoesNotUpdateThem()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7010;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Original Artist Name', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        ArtistEntity? before = await repo.GetByIdAsync(testArtistId);
        string? originalName = before?.Name;

        UpdateArtistEntity patchEntity = new()
        {
            Id = testArtistId,
            Biography = new PatchField<string>("Changed Biography")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        ArtistEntity? after = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(after);
        Assert.Equal(originalName, after!.Name);
        Assert.Equal("Changed Biography", after.Biography);
    }

    [Fact]
    public async Task AddAsync_AddsNewArtist_ReturnsId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        ArtistEntity newArtist = new()
        {
            Name = "Brand New Artist",
            TrackCount = 0,
            AlbumCount = 0,
            LiveCount = 0,
            CompilationCount = 0,
            BestofCount = 0,
            TotalDurationSeconds = 0,
            Disbanded = false,
            IsFavorite = false,
            ListenCount = 0,
            CreatDate = DateTime.UtcNow
        };

        // Act
        long id = await repo.AddAsync(newArtist);

        // Assert
        Assert.True(id > 0);
        ArtistEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Brand New Artist", fetched!.Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingArtist_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7020;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Artist To Update', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);
        artist!.Name = "Updated Artist Name";
        artist.TrackCount = 25;

        // Act
        bool result = await repo.UpdateAsync(artist);

        // Assert
        Assert.True(result);
        ArtistEntity? updated = await repo.GetByIdAsync(testArtistId);
        Assert.Equal("Updated Artist Name", updated?.Name);
        Assert.Equal(25, updated?.TrackCount);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingArtist_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testArtistId = 7021;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate)
            VALUES (@id, 'Artist To Delete', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)",
            new { id = testArtistId, now });

        ArtistEntity? artist = await repo.GetByIdAsync(testArtistId);
        Assert.NotNull(artist);

        // Act
        bool result = await repo.DeleteAsync(artist!);

        // Assert
        Assert.True(result);
        ArtistEntity? deleted = await repo.GetByIdAsync(testArtistId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<ArtistEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Name == "Artist A");
        Assert.Contains(result, a => a.Name == "Artist B");
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        int count = await repo.CountAsync();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        ArtistEntity? result = await repo.GetByIdAsync(999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ReturnsArtist()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        ArtistEntity? result = await repo.GetByNameAsync("Artist A");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Artist A", result!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistentName_ReturnsNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        ArtistEntity? result = await repo.GetByNameAsync("Non Existent Artist");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithBackgroundConnection_ReturnsArtist()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        ArtistEntity? result = await repo.GetByIdAsync(1, RepositoryConnectionKind.Background);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Artist A", result!.Name);
    }

    [Fact]
    public async Task SearchAsync_WithBackgroundConnection_ReturnsMatchingArtists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        ArtistRepository repo = CreateRepository(fixture);

        // Act
        List<IArtistEntity> result = (await repo.SearchAsync("Artist A", RepositoryConnectionKind.Background)).ToList();

        // Assert
        Assert.Single(result);
    }
}