using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class GenreRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private GenreRepository CreateRepository()
    {
        return new GenreRepository(fixture.Connection, fixture.Connection, NullLogger<GenreRepository>.Instance);
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavorite()
    {
        // Arrange
        GenreRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateFavoriteAsync(1, true);

        // Assert
        Assert.True(ok);

        GenreEntity? genre = await repo.GetByIdAsync(1);
        Assert.NotNull(genre);
        Assert.True(genre!.IsFavorite);
    }

    [Fact]
    public async Task UpdateLastListenAsync_IncrementsListenCountAndSetsLastListen()
    {
        // Arrange
        GenreRepository repo = CreateRepository();
        GenreEntity? before = await repo.GetByIdAsync(1);
        int beforeCount = before?.ListenCount ?? 0;

        // Act
        bool ok = await repo.UpdateLastListenAsync(1);

        // Assert
        Assert.True(ok);
        GenreEntity? after = await repo.GetByIdAsync(1);
        Assert.NotNull(after);
        Assert.Equal(beforeCount + 1, after!.ListenCount);
        Assert.NotNull(after.LastListen);
        Assert.True((DateTime.UtcNow - after.LastListen.Value).TotalSeconds < 10);
    }

    [Fact]
    public async Task UpdateStatisticsAsync_UpdatesFields()
    {
        // Arrange
        GenreRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateStatisticsAsync(1, trackCount: 12, artistCount: 5, albumCount: 3, bestOfCount: 1, liveCount: 2, compilationCount: 0, totalDurationSeconds: 9999);

        // Assert
        Assert.True(ok);

        GenreEntity? genre = await repo.GetByIdAsync(1);
        Assert.NotNull(genre);
        Assert.Equal(12, genre!.TrackCount);
        Assert.Equal(5, genre.ArtistCount);
        Assert.Equal(3, genre.AlbumCount);
        Assert.Equal(1, genre.BestofCount);
        Assert.Equal(2, genre.LiveCount);
        Assert.Equal(0, genre.CompilationCount);        
    }

    [Fact]
    public async Task DeleteGenresWithoutTracks_RemovesGenresWithoutTracks()
    {
        // Arrange
        GenreRepository repo = CreateRepository();

        // Ensure at least one track references genreId = 1 so only genre 2 will be deleted
        fixture.Connection.Execute("UPDATE Tracks SET genreId = @g WHERE id = @id", new { g = 1, id = 1 });

        // Act
        int deleted = await repo.DeleteGenresWithoutTracks();

        // Assert
        Assert.Equal(1, deleted);

        var remaining = (await repo.GetAllAsync()).ToList();
        Assert.DoesNotContain(remaining, g => g.Id == 2);
        Assert.Contains(remaining, g => g.Id == 1);
    }
}