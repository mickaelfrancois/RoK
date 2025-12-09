using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using Rok.Domain.Interfaces.Entities;
using Rok.Infrastructure.Repositories;
using Rok.Shared;

namespace Rok.Infrastructure.UnitTests;

public class AlbumRepositoryTests
{
    private static SqliteDatabaseFixture CreateFixture()
    {
        return new SqliteDatabaseFixture();
    }

    private static AlbumRepository CreateRepository(SqliteDatabaseFixture fixture)
    {
        return new AlbumRepository(fixture.Connection, fixture.Connection, NullLogger<AlbumRepository>.Instance);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithNullString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync(null!)).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithWhitespaceString_ReturnsEmptyCollection()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("   ")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WithMatchingName_ReturnsMatchingAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("First")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("The First Album", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ReturnsMatchingAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("FIRST")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("The First Album", result[0].Name);
    }

    [Fact]
    public async Task SearchAsync_WithSpecialCharacters_ReturnsNoResults()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("%_[]")).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByGenreIdAsync_ReturnsGenreAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.GetByGenreIdAsync(1)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Id == 1);
        Assert.Contains(result, a => a.Id == 3);
    }

    [Fact]
    public async Task GetByGenreIdAsync_WithInvalidGenreId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByGenreIdAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByGenreIdAsync(-1));
    }

    [Fact]
    public async Task GetByArtistIdAsync_ReturnsArtistAlbumsOrderedByYearDesc()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.GetByArtistIdAsync(1)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByArtistIdAsync_WithInvalidArtistId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByArtistIdAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetByArtistIdAsync(-1));
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavoriteToTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5001;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Favorite', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        // Act
        bool ok = await repo.UpdateFavoriteAsync(testAlbumId, true);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.True(album!.IsFavorite);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavoriteToFalse()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5002;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album UnFavorite', 0, 0, 0, 5, 1200, 1, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        // Act
        bool ok = await repo.UpdateFavoriteAsync(testAlbumId, false);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.False(album!.IsFavorite);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFavoriteAsync(0, true));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFavoriteAsync(-1, true));
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5009;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Listen', 0, 0, 0, 5, 1200, 0, 3, @now, 1, 1)",
            new { id = testAlbumId, now });

        AlbumEntity? before = await repo.GetByIdAsync(testAlbumId);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(testAlbumId);

        // Assert
        Assert.True(ok);
        AlbumEntity? after = await repo.GetByIdAsync(testAlbumId);
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
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(-1));
    }

    [Fact]
    public async Task UpdateStatisticsAsync_UpdatesTrackCountAndDuration()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5004;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Stats', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        // Act
        bool ok = await repo.UpdateStatisticsAsync(testAlbumId, trackCount: 12, duration: 4800);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.Equal(12, album!.TrackCount);
        Assert.Equal(4800, album.Duration);
    }

    [Fact]
    public async Task UpdateStatisticsAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateStatisticsAsync(0, 10, 3600));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateStatisticsAsync(-1, 10, 3600));
    }

    [Fact]
    public async Task UpdateGetMetaDataLastAttemptAsync_UpdatesLastAttemptDate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5005;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Metadata', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        DateTime beforeNow = DateTime.UtcNow;

        // Act
        bool ok = await repo.UpdateGetMetaDataLastAttemptAsync(testAlbumId);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.NotNull(album!.GetMetaDataLastAttempt);
        Assert.True(album.GetMetaDataLastAttempt >= beforeNow);
        Assert.True((DateTime.UtcNow - album.GetMetaDataLastAttempt.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateGetMetaDataLastAttemptAsync_WithInvalidId_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetMetaDataLastAttemptAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateGetMetaDataLastAttemptAsync(-1));
    }

    [Fact]
    public async Task DeleteOrphansAsync_RemovesAlbumsWithoutTracks()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long orphanAlbumId = 9000;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Orphan Album', 0, 0, 0, 0, 0, 0, 0, @now, 1, 1)",
            new { id = orphanAlbumId, now });

        // Act
        int deleted = await repo.DeleteOrphansAsync();

        // Assert
        Assert.Equal(2, deleted);
        AlbumEntity? orphan = await repo.GetByIdAsync(orphanAlbumId);
        Assert.Null(orphan);
    }

    [Fact]
    public async Task PatchAsync_UpdatesLabelField()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5006;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Patch', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        UpdateAlbumEntity patchEntity = new()
        {
            Id = testAlbumId,
            Label = new PatchField<string>("New Label")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.Equal("New Label", album!.Label);
    }

    [Fact]
    public async Task PatchAsync_UpdatesMultipleFields()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5007;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Multi Patch', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        UpdateAlbumEntity patchEntity = new()
        {
            Id = testAlbumId,
            Label = new PatchField<string>("Universal"),
            Mood = new PatchField<string>("Energetic"),
            Sales = new PatchField<string>("100000"),
            IsLive = new PatchField<bool>(true)
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.Equal("Universal", album!.Label);
        Assert.Equal("Energetic", album.Mood);
        Assert.Equal("100000", album.Sales);
        Assert.True(album.IsLive);
    }

    [Fact]
    public async Task PatchAsync_WithNullValues_SetsFieldsToNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5030;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, label, mood)
            VALUES (@id, 'Test Album Null Patch', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1, 'Original Label', 'Original Mood')",
            new { id = testAlbumId, now });

        UpdateAlbumEntity patchEntity = new()
        {
            Id = testAlbumId,
            Label = new PatchField<string>(null),
            Mood = new PatchField<string>(null)
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.Null(album!.Label);
        Assert.Null(album.Mood);
    }

    [Fact]
    public async Task PatchAsync_WithAllFields_UpdatesAllFields()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5031;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Test Album Full Patch', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        UpdateAlbumEntity patchEntity = new()
        {
            Id = testAlbumId,
            Label = new PatchField<string>("Sony Music"),
            Mood = new PatchField<string>("Relaxing"),
            MusicBrainzID = new PatchField<string>("mb-12345"),
            ReleaseDate = new PatchField<DateTime?>(new DateTime(2020, 5, 15, 0, 0, 0, DateTimeKind.Utc)),
            ReleaseFormat = new PatchField<string>("CD"),
            Sales = new PatchField<string>("50000"),
            Speed = new PatchField<string>("Medium"),
            Theme = new PatchField<string>("Love"),
            Wikipedia = new PatchField<string>("https://en.wikipedia.org/wiki/Album"),
            IsLive = new PatchField<bool>(false),
            IsBestOf = new PatchField<bool>(true),
            IsCompilation = new PatchField<bool>(false)
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        Assert.Equal("Sony Music", album!.Label);
        Assert.Equal("Relaxing", album.Mood);
        Assert.Equal("mb-12345", album.MusicBrainzID);
        Assert.True(album.IsBestOf);
    }

    [Fact]
    public async Task PatchAsync_WithNonExistentAlbum_ReturnsFalse()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        UpdateAlbumEntity patchEntity = new()
        {
            Id = 999999,
            Label = new PatchField<string>("Test")
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
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5008;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Original Name', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        AlbumEntity? before = await repo.GetByIdAsync(testAlbumId);
        string? originalName = before?.Name;

        UpdateAlbumEntity patchEntity = new()
        {
            Id = testAlbumId,
            Label = new PatchField<string>("Changed Label")
        };

        // Act
        bool ok = await repo.PatchAsync(patchEntity);

        // Assert
        Assert.True(ok);
        AlbumEntity? after = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(after);
        Assert.Equal(originalName, after!.Name);
        Assert.Equal("Changed Label", after.Label);
    }

    [Fact]
    public async Task AddAsync_AddsNewAlbum_ReturnsId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        AlbumEntity newAlbum = new()
        {
            Name = "Brand New Album",
            IsLive = false,
            IsCompilation = false,
            IsBestOf = false,
            TrackCount = 0,
            Duration = 0,
            IsFavorite = false,
            ListenCount = 0,
            CreatDate = DateTime.UtcNow,
            ArtistId = 1,
            GenreId = 1
        };

        // Act
        long id = await repo.AddAsync(newAlbum);

        // Assert
        Assert.True(id > 0);
        AlbumEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Brand New Album", fetched!.Name);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingAlbum_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5020;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Album To Update', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);
        album!.Name = "Updated Album Name";
        album.TrackCount = 15;

        // Act
        bool result = await repo.UpdateAsync(album);

        // Assert
        Assert.True(result);
        AlbumEntity? updated = await repo.GetByIdAsync(testAlbumId);
        Assert.Equal("Updated Album Name", updated?.Name);
        Assert.Equal(15, updated?.TrackCount);
    }

    [Fact]
    public async Task DeleteAsync_DeletesExistingAlbum_ReturnsTrue()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;
        long testAlbumId = 5021;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
            VALUES (@id, 'Album To Delete', 0, 0, 0, 5, 1200, 0, 0, @now, 1, 1)",
            new { id = testAlbumId, now });

        AlbumEntity? album = await repo.GetByIdAsync(testAlbumId);
        Assert.NotNull(album);

        // Act
        bool result = await repo.DeleteAsync(album!);

        // Assert
        Assert.True(result);
        AlbumEntity? deleted = await repo.GetByIdAsync(testAlbumId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<AlbumEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, a => a.Name == "The First Album");
        Assert.Contains(result, a => a.Name == "Second Sounds");
        Assert.Contains(result, a => a.Name == "Another Album");
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

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
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        AlbumEntity? result = await repo.GetByIdAsync(999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ReturnsAlbum()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        AlbumEntity? result = await repo.GetByNameAsync("The First Album");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("The First Album", result!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistentName_ReturnsNull()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        AlbumEntity? result = await repo.GetByNameAsync("Non Existent Album");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithBackgroundConnection_ReturnsAlbum()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        AlbumEntity? result = await repo.GetByIdAsync(1, RepositoryConnectionKind.Background);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("The First Album", result!.Name);
    }

    [Fact]
    public async Task SearchAsync_WithBackgroundConnection_ReturnsMatchingAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("First", RepositoryConnectionKind.Background)).ToList();

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetByGenreIdAsync_WithGenreHavingManyAlbums_ReturnsAll()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = CreateFixture();
        AlbumRepository repo = CreateRepository(fixture);
        DateTime now = DateTime.UtcNow;

        for (int i = 0; i < 50; i++)
        {
            await fixture.Connection.ExecuteAsync(@"
                INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId)
                VALUES (@id, @name, 0, 0, 0, 5, 1200, 0, 0, @now, 1, 2)",
                new { id = 6000 + i, name = $"Album {i}", now });
        }

        // Act
        List<IAlbumEntity> result = (await repo.GetByGenreIdAsync(2)).ToList();

        // Assert
        Assert.Equal(51, result.Count);
    }
}