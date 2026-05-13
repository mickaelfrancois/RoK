using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class PlaylistHeaderRepositoryTests
{
    private static PlaylistHeaderRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<PlaylistHeaderRepository>.Instance, TimeProvider.System);

    private static async Task<long> SeedPlaylistAsync(SqliteDatabaseFixture fixture, string name = "My Playlist")
    {
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO Playlists(name, type, creatDate) VALUES (@name, 0, @now)",
            new { name, now = DateTime.UtcNow });
        return await fixture.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
    }

    [Fact(DisplayName = "Add should insert a playlist and return its new id")]
    public async Task Add_ShouldInsertPlaylist_AndReturnNewId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);
        PlaylistHeaderEntity entity = new() { Name = "Added", Type = 1, CreatDate = DateTime.UtcNow };

        // Act
        long id = await repo.AddAsync(entity);

        // Assert
        Assert.True(id > 0);
        PlaylistHeaderEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Added", fetched!.Name);
    }

    [Fact(DisplayName = "GetAll should return every playlist from the database")]
    public async Task GetAll_ShouldReturnEveryPlaylist_FromDatabase()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);
        await SeedPlaylistAsync(fixture, "A");
        await SeedPlaylistAsync(fixture, "B");

        // Act
        List<PlaylistHeaderEntity> result = (await repo.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact(DisplayName = "UpdatePicture should persist the new picture for the playlist")]
    public async Task UpdatePicture_ShouldPersistNewPicture_ForPlaylist()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);
        long id = await SeedPlaylistAsync(fixture);

        // Act
        bool ok = await repo.UpdatePictureAsync(id, "cover.png");

        // Assert
        Assert.True(ok);
        PlaylistHeaderEntity? fetched = await repo.GetByIdAsync(id);
        Assert.Equal("cover.png", fetched!.Picture);
    }

    [Fact(DisplayName = "UpdatePicture should throw when id is not greater than zero")]
    public async Task UpdatePicture_ShouldThrow_WhenIdIsNotGreaterThanZero()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdatePictureAsync(0, "x.png"));
    }

    [Fact(DisplayName = "Delete should remove playlist and its linked tracks in a single transaction")]
    public async Task Delete_ShouldRemovePlaylist_AndLinkedTracks_InSingleTransaction()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO playlisttracks(playlistId, trackId, position, listened, creatDate) VALUES (@playlistId, 1, 0, 0, @now)",
            new { playlistId, now = DateTime.UtcNow });

        // Act
        int affected = await repo.DeleteAsync(playlistId);

        // Assert
        Assert.Equal(1, affected);
        Assert.Null(await repo.GetByIdAsync(playlistId));
        int linkCount = await fixture.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM playlisttracks WHERE playlistid = @playlistId", new { playlistId });
        Assert.Equal(0, linkCount);
    }

    [Fact(DisplayName = "Delete should return zero when playlist does not exist")]
    public async Task Delete_ShouldReturnZero_WhenPlaylistDoesNotExist()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistHeaderRepository repo = CreateRepository(fixture);

        // Act
        int affected = await repo.DeleteAsync(999);

        // Assert
        Assert.Equal(0, affected);
    }
}
