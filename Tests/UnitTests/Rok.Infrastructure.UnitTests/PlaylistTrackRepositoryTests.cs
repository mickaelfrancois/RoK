using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class PlaylistTrackRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private PlaylistTrackRepository CreateRepository()
    {
        return new PlaylistTrackRepository(fixture.Connection, fixture.Connection, NullLogger<PlaylistTrackRepository>.Instance);
    }

    [Fact]
    public async Task AddAsync_AddPlaylistTrack_And_GetAsyncReturnsId()
    {
        // Arrange
        PlaylistTrackRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;

        // create a playlist minimal (type and creatDate are NOT NULL)
        long playlistId = 5000;
        fixture.Connection.Execute("INSERT INTO Playlists(id, name, type, creatDate) VALUES (@id, @name, @type, @now)",
            new { id = playlistId, name = "PT Test", type = 1, now });

        // Act
        var entity = new PlaylistTrackEntity
        {
            PlaylistId = playlistId,
            TrackId = 1,
            Position = 0,
            Listened = false,
            CreatDate = now
        };

        long rows = await repo.AddAsync(entity);
        long fetchedId = await repo.GetAsync(playlistId, 1);

        // Assert
        Assert.Equal(1, rows);
        Assert.True(fetchedId > 0);
    }

    [Fact]
    public async Task DeleteAsync_ByPlaylistId_RemovesAllPlaylistTracks()
    {
        // Arrange
        PlaylistTrackRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        long playlistId = 6000;
        fixture.Connection.Execute("INSERT INTO Playlists(id, name, type, creatDate) VALUES (@id, @name, @type, @now)",
            new { id = playlistId, name = "PT Bulk", type = 1, now });

        // insert two playlisttracks
        fixture.Connection.Execute("INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate) VALUES (9001, @playlistId, 1, 0, 0, @now)", new { playlistId, now });
        fixture.Connection.Execute("INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate) VALUES (9002, @playlistId, 2, 1, 0, @now)", new { playlistId, now });

        int before = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(1) FROM PlaylistTracks WHERE playlistId = @playlistId", new { playlistId });
        Assert.Equal(2, before);

        // Act
        int deleted = (int)await repo.DeleteAsync(playlistId);

        // Assert
        Assert.Equal(2, deleted);
        int after = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(1) FROM PlaylistTracks WHERE playlistId = @playlistId", new { playlistId });
        Assert.Equal(0, after);
    }

    [Fact]
    public async Task DeleteAsync_ByPlaylistAndTrack_DeletesSingleEntry()
    {
        // Arrange
        PlaylistTrackRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        long playlistId = 7000;
        fixture.Connection.Execute("INSERT INTO Playlists(id, name, type, creatDate) VALUES (@id, @name, @type, @now)",
            new { id = playlistId, name = "PT Single", type = 1, now });

        // insert one playlisttrack
        fixture.Connection.Execute("INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate) VALUES (9101, @playlistId, 3, 0, 0, @now)", new { playlistId, now });

        int existing = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(1) FROM PlaylistTracks WHERE playlistId = @playlistId AND trackId = @trackId", new { playlistId, trackId = 3 });
        Assert.Equal(1, existing);

        // Act
        long deleted = await repo.DeleteAsync(playlistId, 3);
        long fetchedId = await repo.GetAsync(playlistId, 3);

        // Assert
        Assert.Equal(1, deleted);
        Assert.Equal(0, fetchedId);
    }
}