using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class GenreRepositoryTests
{
    private static GenreRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<GenreRepository>.Instance);

    [Fact(DisplayName = "GetAll should return all seeded genres")]
    public async Task GetAll_ShouldReturnAllSeededGenres()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act
        List<GenreEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, g => g.Name == "Rock");
        Assert.Contains(result, g => g.Name == "Jazz");
    }

    [Fact(DisplayName = "GetById should return genre when id exists")]
    public async Task GetById_ShouldReturnGenre_WhenIdExists()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act
        GenreEntity? result = await repo.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Rock", result!.Name);
    }

    [Fact(DisplayName = "UpdateFavorite should toggle favorite flag")]
    public async Task UpdateFavorite_ShouldToggleFavoriteFlag()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act
        bool ok = await repo.UpdateFavoriteAsync(1, true);

        // Assert
        Assert.True(ok);
        GenreEntity? genre = await repo.GetByIdAsync(1);
        Assert.True(genre!.IsFavorite);
    }

    [Fact(DisplayName = "UpdateFavorite should throw when id is not greater than zero")]
    public async Task UpdateFavorite_ShouldThrow_WhenIdIsNotGreaterThanZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateFavoriteAsync(0, true));
    }

    [Fact(DisplayName = "UpdateLastListen should increment listen count and set timestamp")]
    public async Task UpdateLastListen_ShouldIncrementListenCount_AndSetTimestamp()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);
        GenreEntity? before = await repo.GetByIdAsync(1);
        int beforeCount = before!.ListenCount;
        DateTime now = DateTime.UtcNow;

        // Act
        bool ok = await repo.UpdateLastListenAsync(1);

        // Assert
        Assert.True(ok);
        GenreEntity? after = await repo.GetByIdAsync(1);
        Assert.Equal(beforeCount + 1, after!.ListenCount);
        Assert.NotNull(after.LastListen);
        Assert.True((DateTime.UtcNow - after.LastListen!.Value).TotalSeconds < 10);
    }

    [Fact(DisplayName = "UpdateLastListen should throw when id is not greater than zero")]
    public async Task UpdateLastListen_ShouldThrow_WhenIdIsNotGreaterThanZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateLastListenAsync(-1));
    }

    [Fact(DisplayName = "ResetListenCount should zero the listen count of every genre")]
    public async Task ResetListenCount_ShouldZeroListenCount_OfEveryGenre()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE genres SET listenCount = 5");

        // Act
        bool ok = await repo.ResetListenCountAsync();

        // Assert
        Assert.True(ok);
        List<GenreEntity> genres = (await repo.GetAllAsync()).ToList();
        Assert.All(genres, g => Assert.Equal(0, g.ListenCount));
    }

    [Fact(DisplayName = "UpdateStatistics should persist aggregate counters")]
    public async Task UpdateStatistics_ShouldPersistAggregateCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act
        bool ok = await repo.UpdateStatisticsAsync(1, trackCount: 10, artistCount: 3, albumCount: 2, bestOfCount: 1, liveCount: 1, compilationCount: 0, totalDurationSeconds: 1800);

        // Assert
        Assert.True(ok);
        GenreEntity? genre = await repo.GetByIdAsync(1);
        Assert.Equal(10, genre!.TrackCount);
        Assert.Equal(3, genre.ArtistCount);
        Assert.Equal(2, genre.AlbumCount);
        Assert.Equal(1, genre.BestofCount);
        Assert.Equal(1, genre.LiveCount);
    }

    [Fact(DisplayName = "DeleteOrphans should remove genres not referenced by any track")]
    public async Task DeleteOrphans_ShouldRemoveGenresNotReferencedByAnyTrack()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        GenreRepository repo = CreateRepository(fixture);

        // Act
        int deleted = await repo.DeleteOrphansAsync();

        // Assert
        Assert.Equal(2, deleted);
        List<GenreEntity> remaining = (await repo.GetAllAsync()).ToList();
        Assert.Empty(remaining);
    }
}
