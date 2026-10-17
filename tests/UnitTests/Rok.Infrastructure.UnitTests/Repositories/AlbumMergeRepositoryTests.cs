using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class AlbumMergeRepositoryTests
{
    private const long TargetId = 10;
    private const long SourceId = 11;
    private const long TargetArtistId = 1;
    private const long SourceArtistId = 2;

    private static readonly DateTime LastListen = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AlbumMergeRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, CreatePendingRepository(fixture), TimeProvider.System, NullLogger<AlbumMergeRepository>.Instance);

    private static PendingTagWriteRepository CreatePendingRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, NullLogger<PendingTagWriteRepository>.Instance);

    private static ArtistRepository CreateArtistRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<ArtistRepository>.Instance, TimeProvider.System);

    private static AlbumRepository CreateAlbumRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<AlbumRepository>.Instance, TimeProvider.System);

    private static GenreRepository CreateGenreRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<GenreRepository>.Instance, TimeProvider.System);

    private static TrackRepository CreateTrackRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<TrackRepository>.Instance, TimeProvider.System);

    private static EqualizerPresetRepository CreatePresetRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, fixture.Connection, NullLogger<EqualizerPresetRepository>.Instance, TimeProvider.System);

    private static async Task SeedAsync(SqliteDatabaseFixture fixture, long sourceArtistId = SourceArtistId)
    {
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync("UPDATE artists SET genreId = 1 WHERE id = 1; UPDATE artists SET genreId = 2 WHERE id = 2");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, year, label, albumPath)
            VALUES
            (10, 'Shared', 0, 0, 0, 99, 99, 0, 2, @now, 1, 1, 2000, NULL, '/music/a/shared'),
            (11, 'Shared', 0, 0, 0, 99, 99, 1, 3, @now, @sourceArtistId, 2, 2000, 'Source Label', '/music/b/shared')", new { now, sourceArtistId });
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber, disc, genreId)
            VALUES
            (10, 't10', 100, 1000, 128, '/f10', @now, 0, 0, 0, 0, @now, 10, 1, 1, 1, 1),
            (13, 't13', 110, 1000, 128, '/f13', @now, 0, 0, 0, 0, @now, 10, 1, 1, 2, 1),
            (11, 't11', 50, 1000, 128, '/f11', @now, 0, 0, 0, 0, @now, 11, @sourceArtistId, 1, 1, 2),
            (14, 't14', 60, 1000, 128, '/f14', @now, 0, 0, 0, 0, @now, 11, @sourceArtistId, 2, NULL, 2),
            (12, 't12', 70, 1000, 128, '/f12', @now, 0, 0, 0, 0, @now, 3, 2, 1, 1, 2)", new { now, sourceArtistId });
        await fixture.Connection.ExecuteAsync("INSERT INTO Tags(id, name) VALUES (1, 'rock'), (2, 'live')");
        await fixture.Connection.ExecuteAsync("INSERT INTO AlbumTags(albumId, tagId) VALUES (10, 1), (11, 1), (11, 2)");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal)
            VALUES
            (10, 1, 10, 1, @now, 0, 100, 100),
            (11, @sourceArtistId, 11, 2, @now, 0, 50, 50),
            (12, 2, 3, 2, @now, 1, 5, 70)", new { now, sourceArtistId });
    }

    private static Task SeedPresetAsync(SqliteDatabaseFixture fixture, long scopeId, double band0) =>
        fixture.Connection.ExecuteAsync(
            "INSERT INTO EqualizerPreset(Scope, ScopeId, Band0) VALUES (@scope, @scopeId, @band0)",
            new { scope = nameof(EqualizerScope.Album), scopeId, band0 });

    private static async Task<AlbumMergeDbChange> BuildChangeAsync(
        SqliteDatabaseFixture fixture,
        int discOffset = 0,
        IReadOnlyList<PendingTagWrite>? pending = null)
    {
        AlbumRepository albums = CreateAlbumRepository(fixture);
        AlbumEntity target = (await albums.GetByIdAsync(TargetId))!;
        AlbumEntity source = (await albums.GetByIdAsync(SourceId))!;
        source.LastListen = LastListen;

        AlbumMergeResult result = AlbumMergeRules.Merge(target, source);

        return new AlbumMergeDbChange(SourceId, TargetId, result.Merged, discOffset, pending ?? []);
    }

    [Fact(DisplayName = "apply_merge_moves_tracks_events_tags_and_fields_of_two_artists_albums_in_one_transaction")]
    public async Task ApplyAsync_InterArtistMerge_MovesEverythingAndRecomputesCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture, discOffset: 2, pending: [new PendingTagWrite(11, ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Disc), new PendingTagWrite(14, ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Disc)]);

        // Act
        await repo.ApplyAsync(change);

        // Assert
        AlbumRepository albums = CreateAlbumRepository(fixture);
        Assert.Null(await albums.GetByIdAsync(SourceId));
        AlbumEntity? target = await albums.GetByIdAsync(TargetId);
        Assert.NotNull(target);
        Assert.Equal("Shared", target!.Name);
        Assert.Equal("/music/a/shared", target.AlbumPath);
        Assert.Equal("Source Label", target.Label);
        Assert.True(target.IsFavorite);
        Assert.Equal(5, target.ListenCount);
        Assert.Equal(LastListen, target.LastListen);
        Assert.Equal(4, target.TrackCount);
        Assert.Equal(320, target.Duration);
        Assert.Equal(3, target.DiscCount);
        Assert.Equal(TargetArtistId, target.ArtistId);

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(new long[] { 10, 11, 13, 14 }, tracks.Where(t => t.AlbumId == TargetId).Select(t => t.Id).Order().ToArray());
        Assert.All(tracks.Where(t => t.AlbumId == TargetId), t => Assert.Equal(TargetArtistId, t.ArtistId));
        Assert.Equal(1, tracks.Single(t => t.Id == 10).Disc);
        Assert.Equal(2, tracks.Single(t => t.Id == 13).Disc);
        Assert.Equal(3, tracks.Single(t => t.Id == 11).Disc);
        Assert.Equal(3, tracks.Single(t => t.Id == 14).Disc);
        Assert.Equal(SourceArtistId, tracks.Single(t => t.Id == 12).ArtistId);

        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @TargetId AND artistId = 1", new { TargetId }));
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE trackId = 12 AND artistId = 2"));
        Assert.Equal(new long[] { 1, 2 }, (await fixture.Connection.QueryAsync<long>("SELECT tagId FROM AlbumTags WHERE albumId = @TargetId ORDER BY tagId", new { TargetId })).ToArray());
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @SourceId", new { SourceId }));

        IReadOnlyList<PendingTagWrite> pending = await CreatePendingRepository(fixture).GetByAlbumAsync(TargetId);
        Assert.Equal(new long[] { 11, 14 }, pending.Select(p => p.TrackId).Order().ToArray());
        Assert.All(pending, p => Assert.Equal(ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Disc, p.Fields));

        ArtistEntity? targetArtist = await CreateArtistRepository(fixture).GetByIdAsync(TargetArtistId);
        Assert.Equal(6, targetArtist!.TrackCount);
        Assert.Equal(700, targetArtist.TotalDurationSeconds);
        Assert.Equal(3, targetArtist.AlbumCount);
        ArtistEntity? sourceArtist = await CreateArtistRepository(fixture).GetByIdAsync(SourceArtistId);
        Assert.Equal(2, sourceArtist!.TrackCount);
        Assert.Equal(310, sourceArtist.TotalDurationSeconds);
        Assert.Equal(1, sourceArtist.AlbumCount);

        GenreRepository genres = CreateGenreRepository(fixture);
        GenreEntity? rock = await genres.GetByIdAsync(1);
        Assert.Equal(2, rock!.TrackCount);
        GenreEntity? jazz = await genres.GetByIdAsync(2);
        Assert.Equal(3, jazz!.TrackCount);
    }

    [Fact(DisplayName = "apply_merge_of_the_same_artist_keeps_artist_and_discs")]
    public async Task ApplyAsync_SameArtistWithoutOffset_KeepsArtistAndDiscs()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture, sourceArtistId: TargetArtistId);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture);

        // Act
        await repo.ApplyAsync(change);

        // Assert
        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.All(tracks.Where(t => t.AlbumId == TargetId), t => Assert.Equal(TargetArtistId, t.ArtistId));
        Assert.Equal(2, tracks.Single(t => t.Id == 13).Disc);
        Assert.Equal(1, tracks.Single(t => t.Id == 11).Disc);
        Assert.Null(tracks.Single(t => t.Id == 14).Disc);
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites"));
        Assert.Null(await CreateAlbumRepository(fixture).GetByIdAsync(SourceId));
    }

    [Fact(DisplayName = "apply_merge_keeps_the_target_preset_and_deletes_the_source_one")]
    public async Task ApplyAsync_BothAlbumsHavePreset_KeepsTargetPresetWithoutUniqueViolation()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, TargetId, 3);
        await SeedPresetAsync(fixture, SourceId, 4);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture);

        // Act
        await repo.ApplyAsync(change);

        // Assert
        EqualizerPresetRepository presets = CreatePresetRepository(fixture);
        Assert.Equal(3f, (await presets.FindAsync(EqualizerScope.Album, TargetId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Album, SourceId));
    }

    [Fact(DisplayName = "apply_merge_moves_the_source_preset_when_the_target_has_none")]
    public async Task ApplyAsync_OnlySourceHasPreset_MovesItToTarget()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, SourceId, 4);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture);

        // Act
        await repo.ApplyAsync(change);

        // Assert
        EqualizerPresetRepository presets = CreatePresetRepository(fixture);
        Assert.Equal(4f, (await presets.FindAsync(EqualizerScope.Album, TargetId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Album, SourceId));
    }

    [Fact(DisplayName = "apply_merge_rolls_back_everything_on_failure")]
    public async Task ApplyAsync_OnFailure_RollsBackEverything()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture, discOffset: 2, pending: [new PendingTagWrite(11, ETagWriteFields.Album), new PendingTagWrite(9999, ETagWriteFields.Album)]);

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => repo.ApplyAsync(change));

        // Assert
        AlbumRepository albums = CreateAlbumRepository(fixture);
        Assert.NotNull(await albums.GetByIdAsync(SourceId));
        AlbumEntity? target = await albums.GetByIdAsync(TargetId);
        Assert.Null(target!.Label);
        Assert.Equal(2, target.ListenCount);
        Assert.Equal(99, target.TrackCount);

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(SourceId, tracks.Single(t => t.Id == 11).AlbumId);
        Assert.Equal(SourceArtistId, tracks.Single(t => t.Id == 11).ArtistId);
        Assert.Equal(1, tracks.Single(t => t.Id == 11).Disc);
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE trackId = 11 AND artistId = 2 AND albumId = 11"));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @SourceId", new { SourceId }));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites"));
    }

    [Fact(DisplayName = "apply_merge_of_an_unknown_album_changes_nothing")]
    public async Task ApplyAsync_UnknownSource_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = (await BuildChangeAsync(fixture)) with { SourceId = 999 };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyAsync(change));

        // Assert
        Assert.NotNull(await CreateAlbumRepository(fixture).GetByIdAsync(SourceId));
    }

    [Fact(DisplayName = "apply_merge_rejects_identical_ids_and_a_negative_offset")]
    public async Task ApplyAsync_InvalidChange_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumMergeRepository repo = CreateRepository(fixture);
        AlbumMergeDbChange change = await BuildChangeAsync(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ApplyAsync(change with { SourceId = TargetId }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ApplyAsync(change with { DiscOffset = -1 }));
    }
}