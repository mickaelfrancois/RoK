using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Domain.Interfaces.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class AlbumRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private AlbumRepository CreateRepository()
    {
        return new AlbumRepository(fixture.Connection, fixture.Connection, NullLogger<AlbumRepository>.Instance);
    }


    [Fact]
    public async Task SearchAsync_ReturnsMatchingAlbums()
    {
        // Arrange
        AlbumRepository repo = CreateRepository();

        // Act
        List<IAlbumEntity> result = (await repo.SearchAsync("First")).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("The First Album", result[0].Name);
    }

    [Fact]
    public async Task GetByGenreIdAsync_ReturnsGenreAlbums()
    {
        // Arrange
        AlbumRepository repo = CreateRepository();

        // Act
        List<IAlbumEntity> result = (await repo.GetByGenreIdAsync(1)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Id == 1);
        Assert.Contains(result, a => a.Id == 3);
    }

    [Fact]
    public async Task GetByArtistIdAsync_OrdersByYearDesc()
    {
        // Arrange
        AlbumRepository repo = CreateRepository();

        // Act
        List<IAlbumEntity> result = (await repo.GetByArtistIdAsync(1)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Id); // 2010
        Assert.Equal(1, result[1].Id); // 2000
    }

    [Fact]
    public async Task UpdateFavoriteAsync_TogglesFavorite()
    {
        // Arrange
        AlbumRepository repo = CreateRepository();

        // Act
        bool ok = await repo.UpdateFavoriteAsync(1, true);

        // Assert
        Assert.True(ok);

        AlbumEntity? album = await repo.GetByIdAsync(1);
        Assert.True(album?.IsFavorite);
    }

    [Fact]
    public async Task DeleteAlbumsWithoutTracks_RemovesAlbumsWithoutTracks()
    {
        // Arrange
        AlbumRepository repo = CreateRepository();

        // Act
        int deleted = await repo.DeleteAlbumsWithoutTracks();

        // Assert
        Assert.Equal(1, deleted);

        List<AlbumEntity> remaining = (await repo.GetAllAsync()).ToList();
        Assert.DoesNotContain(remaining, a => a.Id == 3);
    }
}
