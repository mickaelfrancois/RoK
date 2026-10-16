using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class ArtistMergeRepositoryTests
{
    private const long TargetId = 1;
    private const long SourceId = 2;
    private const long SurvivorAlbumId = 10;
    private const long MergedAlbumId = 11;

    private static readonly DateTime LastListen = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private static ArtistMergeRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, NullLogger<ArtistMergeRepository>.Instance);

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

    private static async Task SeedAsync(SqliteDatabaseFixture fixture)
    {
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync("UPDATE artists SET genreId = 1 WHERE id = 1; UPDATE artists SET genreId = 2 WHERE id = 2");
        await fixture.Connection.ExecuteAsync("UPDATE genres SET artistCount = 1");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, year)
            VALUES
            (10, 'Shared', 0, 0, 0, 99, 99, 0, 2, @now, 1, 1, 2000),
            (11, 'shared', 0, 0, 0, 99, 99, 1, 3, @now, 2, 2, 1990)", new { now });
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES
            (10, 't10', 100, 1000, 128, '/f10', @now, 0, 0, 0, 0, @now, 10, 1, 1),
            (11, 't11', 50, 1000, 128, '/f11', @now, 0, 0, 0, 0, @now, 11, 2, 1),
            (12, 't12', 70, 1000, 128, '/f12', @now, 0, 0, 0, 0, @now, 3, 2, 1)", new { now });
        await fixture.Connection.ExecuteAsync("INSERT INTO Tags(id, name) VALUES (1, 'rock'), (2, 'live'), (3, 'indie')");
        await fixture.Connection.ExecuteAsync("INSERT INTO AlbumTags(albumId, tagId) VALUES (10, 1), (11, 1), (11, 2)");
        await fixture.Connection.ExecuteAsync("INSERT INTO ArtistTags(artistId, tagId) VALUES (1, 2), (2, 1), (2, 2), (2, 3)");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal)
            VALUES
            (10, 1, 10, 1, @now, 0, 100, 100),
            (11, 2, 11, 2, @now, 0, 50, 50),
            (12, 2, 3, 2, @now, 1, 5, 70)", new { now });
    }

    private static Task SeedPresetAsync(SqliteDatabaseFixture fixture, EqualizerScope scope, long scopeId, double band0)
    {
        return fixture.Connection.ExecuteAsync(
            "INSERT INTO EqualizerPreset(Scope, ScopeId, Band0) VALUES (@scope, @scopeId, @band0)",
            new { scope = scope.ToString(), scopeId, band0 });
    }

    private static ArtistMergeDbChange BuildChange(string? pendingFrom = "Artist B", IReadOnlyList<PlaylistCriteriaChange>? criteria = null)
    {
        ArtistEntity merged = new()
        {
            Id = TargetId,
            Name = "Artist A",
            Biography = "Merged biography",
            GenreId = 1,
            IsFavorite = true,
            IsLock = true,
            ListenCount = 7,
            LastListen = LastListen,
            MusicBrainzID = "mbid",
            WikipediaUrl = "https://wiki",
            FormedYear = 1980
        };

        AlbumMergeGroup group = new(SurvivorAlbumId, [MergedAlbumId], "Shared", true, 5, LastListen);

        return new ArtistMergeDbChange(SourceId, "Artist B", TargetId, "Artist A", merged, [group], pendingFrom, criteria ?? []);
    }

    private static async Task<long> SeedPlaylistAsync(SqliteDatabaseFixture fixture, string name, string picture, string? groupsJson)
    {
        await fixture.Connection.ExecuteAsync(
            "INSERT INTO Playlists(name, picture, groupsJson, type, creatDate) VALUES (@name, @picture, @groupsJson, 0, @now)",
            new { name, picture, groupsJson, now = DateTime.UtcNow });

        return await fixture.Connection.ExecuteScalarAsync<long>("SELECT last_insert_rowid()");
    }

    [Fact(DisplayName = "apply_merge_moves_every_reference_and_deletes_the_source_last")]
    public async Task ApplyAsync_MovesEveryReferenceAndDeletesTheSource()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        ArtistMergeRepository repo = CreateRepository(fixture);

        // Act
        await repo.ApplyAsync(BuildChange());

        // Assert
        Assert.Null(await CreateArtistRepository(fixture).GetByIdAsync(SourceId));
        ArtistEntity? target = await CreateArtistRepository(fixture).GetByIdAsync(TargetId);
        Assert.NotNull(target);
        Assert.Equal("Artist A", target!.Name);
        Assert.Equal("Artist B", target.RenamePendingFrom);
        Assert.Equal("Merged biography", target.Biography);
        Assert.Equal("mbid", target.MusicBrainzID);
        Assert.Equal("https://wiki", target.WikipediaUrl);
        Assert.Equal(1980, target.FormedYear);
        Assert.True(target.IsFavorite);
        Assert.True(target.IsLock);
        Assert.Equal(7, target.ListenCount);
        Assert.Equal(LastListen, target.LastListen);

        IEnumerable<TrackEntity> tracks = await CreateTrackRepository(fixture).GetByArtistIdAsync(TargetId);
        Assert.Equal(new long[] { 1L, 2L, 3L, 10L, 11L, 12L }, tracks.Select(t => t.Id).Order().ToArray());
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM tracks WHERE artistId = @SourceId", new { SourceId }));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM albums WHERE artistId = @SourceId", new { SourceId }));
        Assert.Equal(3, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents"));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE artistId IS NULL OR albumId IS NULL"));
        Assert.Equal(3, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE artistId = @TargetId", new { TargetId }));
        Assert.Equal(new long[] { 1L, 2L, 3L }, (await fixture.Connection.QueryAsync<long>("SELECT tagId FROM ArtistTags WHERE artistId = @TargetId ORDER BY tagId", new { TargetId })).ToArray());
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ArtistTags WHERE artistId = @SourceId", new { SourceId }));
    }

    [Fact(DisplayName = "apply_merge_merges_homonym_albums")]
    public async Task ApplyAsync_MergesHomonymAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, EqualizerScope.Album, MergedAlbumId, 4);
        ArtistMergeRepository repo = CreateRepository(fixture);

        // Act
        await repo.ApplyAsync(BuildChange());

        // Assert
        AlbumRepository albums = CreateAlbumRepository(fixture);
        Assert.Null(await albums.GetByIdAsync(MergedAlbumId));
        AlbumEntity? survivor = await albums.GetByIdAsync(SurvivorAlbumId);
        Assert.NotNull(survivor);
        Assert.Equal(TargetId, survivor!.ArtistId);
        Assert.True(survivor.IsFavorite);
        Assert.Equal(5, survivor.ListenCount);
        Assert.Equal(LastListen, survivor.LastListen);
        Assert.Equal(new long[] { 10L, 11L }, (await fixture.Connection.QueryAsync<long>("SELECT id FROM tracks WHERE albumId = @SurvivorAlbumId ORDER BY id", new { SurvivorAlbumId })).ToArray());
        Assert.Equal(new long[] { 1L, 2L }, (await fixture.Connection.QueryAsync<long>("SELECT tagId FROM AlbumTags WHERE albumId = @SurvivorAlbumId ORDER BY tagId", new { SurvivorAlbumId })).ToArray());
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @MergedAlbumId", new { MergedAlbumId }));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE albumId = @SurvivorAlbumId", new { SurvivorAlbumId }));
    }

    [Fact(DisplayName = "apply_merge_keeps_the_target_preset_and_drops_the_source_one")]
    public async Task ApplyAsync_KeepsTheTargetPresetAndDropsTheSourceOne()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, EqualizerScope.Artist, TargetId, 1);
        await SeedPresetAsync(fixture, EqualizerScope.Artist, SourceId, 2);
        await SeedPresetAsync(fixture, EqualizerScope.Album, SurvivorAlbumId, 3);
        await SeedPresetAsync(fixture, EqualizerScope.Album, MergedAlbumId, 4);

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange());

        // Assert
        EqualizerPresetRepository presets = CreatePresetRepository(fixture);
        Assert.Equal(1f, (await presets.FindAsync(EqualizerScope.Artist, TargetId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Artist, SourceId));
        Assert.Equal(3f, (await presets.FindAsync(EqualizerScope.Album, SurvivorAlbumId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Album, MergedAlbumId));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EqualizerPreset"));
    }

    [Fact(DisplayName = "apply_merge_moves_the_source_preset_when_the_target_has_none")]
    public async Task ApplyAsync_MovesTheSourcePresetWhenTheTargetHasNone()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, EqualizerScope.Artist, SourceId, 2);
        await SeedPresetAsync(fixture, EqualizerScope.Album, MergedAlbumId, 4);

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange());

        // Assert
        EqualizerPresetRepository presets = CreatePresetRepository(fixture);
        Assert.Equal(2f, (await presets.FindAsync(EqualizerScope.Artist, TargetId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Artist, SourceId));
        Assert.Equal(4f, (await presets.FindAsync(EqualizerScope.Album, SurvivorAlbumId))!.Bands[0]);
        Assert.Null(await presets.FindAsync(EqualizerScope.Album, MergedAlbumId));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EqualizerPreset"));
    }

    [Fact(DisplayName = "apply_merge_recomputes_counters")]
    public async Task ApplyAsync_RecomputesCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE albums SET isCompilation = 1 WHERE id = 3");
        await fixture.Connection.ExecuteAsync("UPDATE albums SET isLive = 1 WHERE id = 2");

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange());

        // Assert
        ArtistEntity? target = await CreateArtistRepository(fixture).GetByIdAsync(TargetId);
        Assert.NotNull(target);
        Assert.Equal(6, target!.TrackCount);
        Assert.Equal(840, target.TotalDurationSeconds);
        Assert.Equal(2, target.AlbumCount);
        Assert.Equal(1, target.LiveCount);
        Assert.Equal(1, target.CompilationCount);
        Assert.Equal(0, target.BestofCount);
        Assert.Equal(2000, target.YearMini);
        Assert.Equal(2000, target.YearMaxi);

        AlbumEntity? survivor = await CreateAlbumRepository(fixture).GetByIdAsync(SurvivorAlbumId);
        Assert.Equal(2, survivor!.TrackCount);
        Assert.Equal(150, survivor.Duration);

        GenreRepository genres = CreateGenreRepository(fixture);
        Assert.Equal(1, (await genres.GetByIdAsync(1))!.ArtistCount);
        Assert.Equal(0, (await genres.GetByIdAsync(2))!.ArtistCount);
    }

    [Fact(DisplayName = "apply_merge_recomputes_the_album_counters_of_the_genres_of_merged_albums")]
    public async Task ApplyAsync_RecomputesTheAlbumCountersOfTheGenresOfMergedAlbums()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await fixture.Connection.ExecuteAsync("UPDATE genres SET albumCount = 50, bestofCount = 50, liveCount = 50, compilationCount = 50 WHERE id = 2");
        int expectedAlbums = await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM albums WHERE genreId = 2 AND id <> 11 AND isCompilation = 0 AND isLive = 0 AND isBestof = 0");

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange());

        // Assert
        GenreEntity? genre = await CreateGenreRepository(fixture).GetByIdAsync(2);
        Assert.NotNull(genre);
        Assert.Equal(expectedAlbums, genre!.AlbumCount);
        Assert.Equal(0, genre.BestofCount);
        Assert.Equal(0, genre.LiveCount);
        Assert.Equal(0, genre.CompilationCount);
    }

    [Fact(DisplayName = "apply_merge_rewrites_playlist_picture_and_criteria")]
    public async Task ApplyAsync_RewritesPlaylistPictureAndCriteria()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        long pictureId = await SeedPlaylistAsync(fixture, "Mix", "artist b", "[]");
        long otherPictureId = await SeedPlaylistAsync(fixture, "Other", "Artist C", "[]");
        long criteriaId = await SeedPlaylistAsync(fixture, "Smart", "", "[{\"old\":1}]");
        long untouchedId = await SeedPlaylistAsync(fixture, "Smart2", "", "[{\"keep\":1}]");

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange(criteria: [new PlaylistCriteriaChange(criteriaId, "[{\"new\":1}]")]));

        // Assert
        Assert.Equal("Artist A", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @pictureId", new { pictureId }));
        Assert.Equal("Artist C", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @otherPictureId", new { otherPictureId }));
        Assert.Equal("[{\"new\":1}]", await fixture.Connection.ExecuteScalarAsync<string>("SELECT groupsJson FROM playlists WHERE id = @criteriaId", new { criteriaId }));
        Assert.Equal("[{\"keep\":1}]", await fixture.Connection.ExecuteScalarAsync<string>("SELECT groupsJson FROM playlists WHERE id = @untouchedId", new { untouchedId }));
    }

    [Fact(DisplayName = "apply_merge_sets_no_marker_when_no_file_is_left_to_write")]
    public async Task ApplyAsync_WithoutPendingFrom_LeavesNoMarker()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);

        // Act
        await CreateRepository(fixture).ApplyAsync(BuildChange(pendingFrom: null));

        // Assert
        ArtistEntity? target = await CreateArtistRepository(fixture).GetByIdAsync(TargetId);
        Assert.Null(target!.RenamePendingFrom);
    }

    [Fact(DisplayName = "apply_merge_rolls_back_everything_on_failure")]
    public async Task ApplyAsync_OnFailure_RollsBackEverything()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedPresetAsync(fixture, EqualizerScope.Artist, SourceId, 2);
        long pictureId = await SeedPlaylistAsync(fixture, "Mix", "Artist B", "[]");
        await fixture.Connection.ExecuteAsync("CREATE TRIGGER fail_delete BEFORE DELETE ON artists BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        ArtistMergeRepository repo = CreateRepository(fixture);

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => repo.ApplyAsync(BuildChange()));

        // Assert
        ArtistEntity? source = await CreateArtistRepository(fixture).GetByIdAsync(SourceId);
        Assert.NotNull(source);
        ArtistEntity? target = await CreateArtistRepository(fixture).GetByIdAsync(TargetId);
        Assert.Null(target!.RenamePendingFrom);
        Assert.Null(target.Biography);
        Assert.Equal(0, target.ListenCount);
        Assert.Equal(new long[] { 3L, 11L, 12L }, (await fixture.Connection.QueryAsync<long>("SELECT id FROM tracks WHERE artistId = @SourceId ORDER BY id", new { SourceId })).ToArray());
        Assert.NotNull(await CreateAlbumRepository(fixture).GetByIdAsync(MergedAlbumId));
        Assert.Equal(99, (await CreateAlbumRepository(fixture).GetByIdAsync(SurvivorAlbumId))!.TrackCount);
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE artistId = @SourceId", new { SourceId }));
        Assert.Equal(3, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ArtistTags WHERE artistId = @SourceId", new { SourceId }));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AlbumTags WHERE albumId = @MergedAlbumId", new { MergedAlbumId }));
        Assert.NotNull(await CreatePresetRepository(fixture).FindAsync(EqualizerScope.Artist, SourceId));
        Assert.Equal("Artist B", await fixture.Connection.ExecuteScalarAsync<string>("SELECT picture FROM playlists WHERE id = @pictureId", new { pictureId }));
    }

    [Fact(DisplayName = "apply_merge_fails_for_an_unknown_target")]
    public async Task ApplyAsync_UnknownTarget_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        ArtistMergeDbChange change = BuildChange() with { TargetId = 999 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRepository(fixture).ApplyAsync(change));
        Assert.NotNull(await CreateArtistRepository(fixture).GetByIdAsync(SourceId));
    }

    [Fact(DisplayName = "apply_merge_refuses_the_same_artist_on_both_sides")]
    public async Task ApplyAsync_SameArtist_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        ArtistMergeDbChange change = BuildChange() with { SourceId = TargetId };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => CreateRepository(fixture).ApplyAsync(change));
    }

    private static async Task SeedCompilationAsync(SqliteDatabaseFixture fixture)
    {
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync("UPDATE genres SET compilationCount = 1 WHERE id = 2");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId, year)
            VALUES (12, 'Same Folder', 0, 1, 0, 2, 20, 1, 4, @now, 2, 2, 1995)", new { now });
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber)
            VALUES
            (13, 't13', 10, 1000, 128, '/f13', @now, 0, 0, 0, 0, @now, 12, 1, 1),
            (14, 't14', 10, 1000, 128, '/f14', @now, 0, 0, 0, 0, @now, 12, 2, 2)", new { now });
    }

    [Fact(DisplayName = "apply_merge_converts_a_single_artist_compilation_into_a_normal_album_of_the_target")]
    public async Task ApplyAsync_ConvertsTheCompilation_AndRecomputesCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedCompilationAsync(fixture);
        ArtistMergeDbChange change = BuildChange() with { ConvertedCompilationIds = [12] };

        // Act
        await CreateRepository(fixture).ApplyAsync(change);

        // Assert
        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(12);
        Assert.NotNull(album);
        Assert.False(album!.IsCompilation);
        Assert.Equal(TargetId, album.ArtistId);
        Assert.True(album.IsFavorite);
        Assert.Equal(4, album.ListenCount);

        ArtistEntity? target = await CreateArtistRepository(fixture).GetByIdAsync(TargetId);
        Assert.Equal(0, target!.CompilationCount);
        Assert.Equal(await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM albums WHERE artistId = 1 AND isCompilation = 0 AND isLive = 0 AND isBestof = 0"), target.AlbumCount);
        Assert.Equal(1995, target.YearMini);
        Assert.Equal(0, (await CreateGenreRepository(fixture).GetByIdAsync(2))!.CompilationCount);
    }

    [Fact(DisplayName = "apply_merge_folds_a_converted_compilation_into_its_homonym")]
    public async Task ApplyAsync_FoldsTheConvertedCompilationIntoItsHomonym()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedCompilationAsync(fixture);
        await fixture.Connection.ExecuteAsync("INSERT INTO AlbumTags(albumId, tagId) VALUES (12, 3)");
        AlbumMergeGroup group = new(SurvivorAlbumId, [MergedAlbumId, 12], "Shared", true, 9, LastListen);
        ArtistMergeDbChange change = BuildChange() with { AlbumGroups = [group], ConvertedCompilationIds = [12] };

        // Act
        await CreateRepository(fixture).ApplyAsync(change);

        // Assert
        AlbumRepository albums = CreateAlbumRepository(fixture);
        Assert.Null(await albums.GetByIdAsync(12));
        Assert.False((await albums.GetByIdAsync(SurvivorAlbumId))!.IsCompilation);
        Assert.Equal(new long[] { 10L, 11L, 13L, 14L }, (await fixture.Connection.QueryAsync<long>("SELECT id FROM tracks WHERE albumId = @SurvivorAlbumId ORDER BY id", new { SurvivorAlbumId })).ToArray());
        Assert.Equal(new long[] { 1L, 2L, 3L }, (await fixture.Connection.QueryAsync<long>("SELECT tagId FROM AlbumTags WHERE albumId = @SurvivorAlbumId ORDER BY tagId", new { SurvivorAlbumId })).ToArray());
        Assert.Equal(0, (await CreateArtistRepository(fixture).GetByIdAsync(TargetId))!.CompilationCount);
    }

    [Fact(DisplayName = "apply_merge_rolls_back_the_compilation_conversion_on_failure")]
    public async Task ApplyAsync_OnFailure_KeepsTheCompilation()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        await SeedCompilationAsync(fixture);
        await fixture.Connection.ExecuteAsync("CREATE TRIGGER fail_delete BEFORE DELETE ON artists BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        ArtistMergeDbChange change = BuildChange() with { ConvertedCompilationIds = [12] };

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => CreateRepository(fixture).ApplyAsync(change));

        // Assert
        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(12);
        Assert.True(album!.IsCompilation);
        Assert.Equal(SourceId, album.ArtistId);
        Assert.Equal(1, (await CreateGenreRepository(fixture).GetByIdAsync(2))!.CompilationCount);
    }
}