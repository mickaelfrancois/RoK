using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests;

public class PlaylistHeaderRepositoryTests(SqliteDatabaseFixture fixture) : IClassFixture<SqliteDatabaseFixture>
{
    private PlaylistHeaderRepository CreateRepository()
    {
        return new PlaylistHeaderRepository(fixture.Connection, fixture.Connection, NullLogger<PlaylistHeaderRepository>.Instance);
    }

    [Fact]
    public async Task AddAsync_AddsPlaylistAndCanBeFetched()
    {
        // Arrange
        PlaylistHeaderRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        var entity = new PlaylistHeaderEntity
        {
            Name = "My Playlist",
            Picture = "/p.png",
            TrackCount = 0,
            Duration = 0,
            TrackMaximum = 100,
            DurationMaximum = 0,
            GroupsJson = null,
            Type = 1,
            CreatDate = now
        };

        // Act
        long id = await repo.AddAsync(entity);

        // Assert
        Assert.True(id > 0);
        PlaylistHeaderEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("My Playlist", fetched!.Name);
        Assert.Equal("/p.png", fetched.Picture);
    }

    [Fact]
    public async Task UpdatePictureAsync_UpdatesPicture()
    {
        // Arrange
        PlaylistHeaderRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        var entity = new PlaylistHeaderEntity
        {
            Name = "Pic Playlist",
            Picture = "/old.png",
            TrackCount = 0,
            Duration = 0,
            TrackMaximum = 10,
            DurationMaximum = 0,
            GroupsJson = null,
            Type = 1,
            CreatDate = now
        };

        long id = await repo.AddAsync(entity);

        // Act
        bool ok = await repo.UpdatePictureAsync(id, "/new.png");

        // Assert
        Assert.True(ok);
        PlaylistHeaderEntity? fetched = await repo.GetByIdAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("/new.png", fetched!.Picture);
    }

    [Fact]
    public async Task DeleteAsync_DeletesPlaylistAndPlaylistTracks()
    {
        // Arrange
        PlaylistHeaderRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        var entity = new PlaylistHeaderEntity
        {
            Name = "ToDelete",
            Picture = null,
            TrackCount = 0,
            Duration = 0,
            TrackMaximum = 10,
            DurationMaximum = 0,
            GroupsJson = null,
            Type = 1,
            CreatDate = now
        };

        long playlistId = await repo.AddAsync(entity);

        // Add two playlist tracks linked to this playlist
        long pt1 = 9001;
        long pt2 = 9002;
        fixture.Connection.Execute("INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate) VALUES (@id, @playlistId, @trackId, @pos, @listened, @now)",
            new { id = pt1, playlistId, trackId = 1, pos = 0, listened = 0, now });
        fixture.Connection.Execute("INSERT INTO PlaylistTracks(id, playlistId, trackId, position, listened, creatDate) VALUES (@id, @playlistId, @trackId, @pos, @listened, @now)",
            new { id = pt2, playlistId, trackId = 2, pos = 1, listened = 0, now });

        // Verify they exist
        int beforeCount = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(1) FROM PlaylistTracks WHERE playlistId = @playlistId", new { playlistId });
        Assert.Equal(2, beforeCount);

        // Act
        int affectedPlaylists = await repo.DeleteAsync(playlistId);

        // Assert
        Assert.Equal(1, affectedPlaylists);
        PlaylistHeaderEntity? remainingPlaylist = await repo.GetByIdAsync(playlistId);
        Assert.Null(remainingPlaylist);

        int afterCount = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(1) FROM PlaylistTracks WHERE playlistId = @playlistId", new { playlistId });
        Assert.Equal(0, afterCount);
    }

    [Fact]
    public async Task GetAllAndGetByName_WorkAsExpected()
    {
        // Arrange
        PlaylistHeaderRepository repo = CreateRepository();
        DateTime now = DateTime.UtcNow;
        var entity = new PlaylistHeaderEntity
        {
            Name = "LookupPlaylist",
            Picture = null,
            TrackCount = 0,
            Duration = 0,
            TrackMaximum = 5,
            DurationMaximum = 0,
            GroupsJson = null,
            Type = 2,
            CreatDate = now
        };

        long id = await repo.AddAsync(entity);

        // Act
        var all = (await repo.GetAllAsync()).ToList();
        PlaylistHeaderEntity? byId = await repo.GetByIdAsync(id);
        PlaylistHeaderEntity? byName = await repo.GetByNameAsync("LookupPlaylist");

        // Assert
        Assert.Contains(all, p => p.Id == id);
        Assert.NotNull(byId);
        Assert.Equal("LookupPlaylist", byId!.Name);
        Assert.NotNull(byName);
        Assert.Equal(id, byName!.Id);
    }
}