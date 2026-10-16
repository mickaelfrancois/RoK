using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Artists.Rename;
using Rok.Domain.Entities;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class ArtistRenameRepositoryTests
{
    private static ArtistRenameRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, NullLogger<ArtistRenameRepository>.Instance);

    private static ArtistRepository CreateArtistRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);

    private static async Task<long> SeedPlaylistAsync(SqliteDatabaseFixture fixture, string name, string picture, string? groupsJson)
    {
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO Playlists(name, picture, groupsJson, type, creatDate) VALUES (@name, @picture, @groupsJson, 0, @now)",
            new { name, picture, groupsJson, now = DateTime.UtcNow });

        return await fixture.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
    }

    [Fact(DisplayName = "apply_rename_updates_name_marker_picture_and_criteria_in_one_transaction")]
    public async Task ApplyAsync_UpdatesNameMarkerPictureAndCriteria()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistRenameRepository repo = CreateRepository(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE artists SET getMetaDataLastAttempt = @now WHERE id = 1", new { now = DateTime.UtcNow });
        long pictureId = await SeedPlaylistAsync(fixture, "Mix", "Artist A", "[]");
        long otherPictureId = await SeedPlaylistAsync(fixture, "Other", "Artist B", "[]");
        long criteriaId = await SeedPlaylistAsync(fixture, "Smart", "", "[{\"old\":1}]");
        long untouchedId = await SeedPlaylistAsync(fixture, "Smart2", "", "[{\"keep\":1}]");
        ArtistRenameDbChange change = new(1, "Artist A", "Artist Renamed", [new PlaylistCriteriaChange(criteriaId, "[{\"new\":1}]")]);

        // Act
        await repo.ApplyAsync(change);

        // Assert
        ArtistEntity? artist = await CreateArtistRepository(fixture).GetByIdAsync(1);
        Assert.NotNull(artist);
        Assert.Equal("Artist Renamed", artist!.Name);
        Assert.Equal("Artist A", artist.RenamePendingFrom);
        Assert.Null(artist.GetMetaDataLastAttempt);
        Assert.Equal("Artist Renamed", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @pictureId", new { pictureId }));
        Assert.Equal("Artist B", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @otherPictureId", new { otherPictureId }));
        Assert.Equal("[{\"new\":1}]", await fixture.Connection.ExecuteScalarAsync<string>("SELECT groupsJson FROM playlists WHERE id = @criteriaId", new { criteriaId }));
        Assert.Equal("[{\"keep\":1}]", await fixture.Connection.ExecuteScalarAsync<string>("SELECT groupsJson FROM playlists WHERE id = @untouchedId", new { untouchedId }));
    }

    [Fact(DisplayName = "apply_rename_keeps_the_first_marker_when_replayed")]
    public async Task ApplyAsync_Replayed_KeepsTheFirstMarker()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistRenameRepository repo = CreateRepository(fixture);
        await repo.ApplyAsync(new ArtistRenameDbChange(1, "Artist A", "Artist Renamed", []));

        // Act
        await repo.ApplyAsync(new ArtistRenameDbChange(1, "Artist Renamed", "Artist Renamed", []));

        // Assert
        ArtistEntity? artist = await CreateArtistRepository(fixture).GetByIdAsync(1);
        Assert.Equal("Artist A", artist!.RenamePendingFrom);
    }

    [Fact(DisplayName = "apply_rename_rolls_back_everything_on_failure")]
    public async Task ApplyAsync_OnFailure_RollsBackEverything()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistRenameRepository repo = CreateRepository(fixture);
        long pictureId = await SeedPlaylistAsync(fixture, "Mix", "Artist A", "[]");
        long criteriaId = await SeedPlaylistAsync(fixture, "Smart", "", "[{\"old\":1}]");
        await fixture.Connection.ExecuteAsync("CREATE TRIGGER fail_criteria BEFORE UPDATE OF groupsJson ON playlists BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        ArtistRenameDbChange change = new(1, "Artist A", "Artist Renamed", [new PlaylistCriteriaChange(criteriaId, "[{\"new\":1}]")]);

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => repo.ApplyAsync(change));

        // Assert
        ArtistEntity? artist = await CreateArtistRepository(fixture).GetByIdAsync(1);
        Assert.Equal("Artist A", artist!.Name);
        Assert.Null(artist.RenamePendingFrom);
        Assert.Equal("Artist A", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @pictureId", new { pictureId }));
        Assert.Equal("[{\"old\":1}]", await fixture.Connection.ExecuteScalarAsync<string>("SELECT groupsJson FROM playlists WHERE id = @criteriaId", new { criteriaId }));
    }

    [Fact(DisplayName = "apply_rename_fails_for_an_unknown_artist")]
    public async Task ApplyAsync_UnknownArtist_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistRenameRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyAsync(new ArtistRenameDbChange(999, "X", "Y", [])));
    }

    [Fact(DisplayName = "complete_clears_the_marker_and_keeps_the_name")]
    public async Task CompleteAsync_ClearsTheMarker()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistRenameRepository repo = CreateRepository(fixture);
        await repo.ApplyAsync(new ArtistRenameDbChange(1, "Artist A", "Artist Renamed", []));

        // Act
        bool completed = await repo.CompleteAsync(1);

        // Assert
        Assert.True(completed);
        ArtistEntity? artist = await CreateArtistRepository(fixture).GetByIdAsync(1);
        Assert.Null(artist!.RenamePendingFrom);
        Assert.Equal("Artist Renamed", artist.Name);
    }
}