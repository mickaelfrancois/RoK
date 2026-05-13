using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class PlaylistTrackRepositoryTests
{
    private static PlaylistTrackRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<PlaylistTrackRepository>.Instance, TimeProvider.System);

    private static async Task<long> SeedPlaylistAsync(SqliteDatabaseFixture fixture)
    {
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO Playlists(name, type, creatDate) VALUES ('P', 0, @now)",
            new { now = DateTime.UtcNow });
        return await fixture.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
    }

    [Fact(DisplayName = "Add should insert a playlist track row")]
    public async Task Add_ShouldInsertPlaylistTrackRow()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);

        // Act
        long rowsAffected = await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 1, Position = 0 });

        // Assert
        Assert.Equal(1, rowsAffected);
        int count = await fixture.Connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM playlisttracks WHERE playlistid = @playlistId AND trackid = 1",
            new { playlistId });
        Assert.Equal(1, count);
    }

    [Fact(DisplayName = "GetAsync by playlist and track id should return link row id")]
    public async Task GetAsync_ByPlaylistAndTrackId_ShouldReturnLinkRowId()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 2, Position = 0 });

        // Act
        long id = await repo.GetAsync(playlistId, 2);

        // Assert
        Assert.True(id > 0);
    }

    [Fact(DisplayName = "GetAsync by playlist and track id should return zero when not linked")]
    public async Task GetAsync_ByPlaylistAndTrackId_ShouldReturnZero_WhenNotLinked()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);

        // Act
        long id = await repo.GetAsync(playlistId, 999);

        // Assert
        Assert.Equal(0, id);
    }

    [Fact(DisplayName = "GetAsync by playlist should return every linked track for playlist")]
    public async Task GetAsync_ByPlaylist_ShouldReturnEveryLinkedTrack_ForPlaylist()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 1, Position = 0 });
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 2, Position = 1 });

        // Act
        List<PlaylistTrackEntity> result = (await repo.GetAsync(playlistId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact(DisplayName = "UpdatePosition should persist new position for a track link")]
    public async Task UpdatePosition_ShouldPersistNewPosition_ForTrackLink()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 1, Position = 0 });
        long linkId = await repo.GetAsync(playlistId, 1);

        // Act
        long ok = await repo.UpdatePositionAsync(linkId, 9);

        // Assert
        Assert.Equal(1, ok);
        int position = await fixture.Connection.ExecuteScalarAsync<int>(
            "SELECT position FROM playlisttracks WHERE id = @id", new { id = linkId });
        Assert.Equal(9, position);
    }

    [Fact(DisplayName = "Delete by playlist id should remove every track from that playlist only")]
    public async Task Delete_ByPlaylistId_ShouldRemoveEveryTrack_FromThatPlaylistOnly()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistA = await SeedPlaylistAsync(fixture);
        long playlistB = await SeedPlaylistAsync(fixture);
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistA, TrackId = 1 });
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistA, TrackId = 2 });
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistB, TrackId = 1 });

        // Act
        long deleted = await repo.DeleteAsync(playlistA);

        // Assert
        Assert.Equal(2, deleted);
        Assert.Empty(await repo.GetAsync(playlistA));
        Assert.Single(await repo.GetAsync(playlistB));
    }

    [Fact(DisplayName = "Delete by playlist and track id should remove a single link")]
    public async Task Delete_ByPlaylistAndTrackId_ShouldRemoveSingleLink()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        PlaylistTrackRepository repo = CreateRepository(fixture);
        long playlistId = await SeedPlaylistAsync(fixture);
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 1 });
        await repo.AddAsync(new PlaylistTrackEntity { PlaylistId = playlistId, TrackId = 2 });

        // Act
        long deleted = await repo.DeleteAsync(playlistId, 1);

        // Assert
        Assert.Equal(1, deleted);
        Assert.Single(await repo.GetAsync(playlistId));
    }
}
