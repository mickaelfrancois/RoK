using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.Domain.Enums;
using Rok.Infrastructure.Repositories;

namespace Rok.Infrastructure.UnitTests.Repositories;

public class AlbumEditRepositoryTests
{
    private const long AlbumId = 1;
    private const long OldArtistId = 1;
    private const long OtherArtistId = 2;
    private const long OldGenreId = 1;
    private const long JazzGenreId = 2;
    private const long OwnGenreId = 3;

    private static AlbumEditRepository CreateRepository(SqliteDatabaseFixture fixture) =>
        new(fixture.Connection, CreatePendingRepository(fixture), TimeProvider.System, NullLogger<AlbumEditRepository>.Instance);

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

    private static async Task SeedAsync(SqliteDatabaseFixture fixture)
    {
        DateTime now = DateTime.UtcNow;

        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Genres(id, name, totalDurationSeconds, trackCount, artistCount, compilationCount, bestofCount, albumCount, liveCount, listenCount, isFavorite, creatDate)
            VALUES (3, 'Blues', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)", new { now });
        await fixture.Connection.ExecuteAsync("UPDATE albums SET year = 1999 WHERE id = 1");
        await fixture.Connection.ExecuteAsync("UPDATE tracks SET genreId = 1 WHERE id IN (1, 2)");
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber, genreId)
            VALUES (4, 't4', 50, 1000, 128, '/f4', @now, 0, 0, 0, 0, @now, 1, 2, 3, 3)", new { now });
        await fixture.Connection.ExecuteAsync(@"
            INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal)
            VALUES
            (1, 1, 1, 1, @now, 0, 180, 180),
            (4, 2, 1, 3, @now, 0, 50, 50)", new { now });
    }

    private static AlbumEditDbChange BuildChange(
        string name = "Renamed",
        int? year = 2005,
        long? newArtistId = OtherArtistId,
        string? newArtistName = null,
        long? newGenreId = JazzGenreId,
        string? newGenreName = null,
        params PendingTagWrite[] pending)
    {
        ETagWriteFields all = ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Year | ETagWriteFields.Genre;
        PendingTagWrite[] writes = pending.Length > 0 ? pending : [new PendingTagWrite(1, all), new PendingTagWrite(2, all)];

        return new AlbumEditDbChange(AlbumId, name, year, OldArtistId, newArtistId, newArtistName, OldGenreId, newGenreId, newGenreName, writes);
    }

    private static Task<long> TotalDurationOfGenreAsync(SqliteDatabaseFixture fixture, long genreId) =>
        fixture.Connection.ExecuteScalarAsync<long>("SELECT totalDurationSeconds FROM genres WHERE id = @genreId", new { genreId });

    [Fact(DisplayName = "apply_edit_updates_album_tracks_events_pending_rows_and_counters_in_one_transaction")]
    public async Task ApplyAsync_UpdatesAlbumTracksEventsPendingRowsAndCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumEditRepository repo = CreateRepository(fixture);

        // Act
        AlbumEditApplied applied = await repo.ApplyAsync(BuildChange());

        // Assert
        Assert.Equal(new AlbumEditApplied(OtherArtistId, JazzGenreId), applied);

        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(AlbumId);
        Assert.NotNull(album);
        Assert.Equal("Renamed", album!.Name);
        Assert.Equal(OtherArtistId, album.ArtistId);
        Assert.Equal(JazzGenreId, album.GenreId);
        Assert.Equal(2005, album.Year);
        Assert.Equal(3, album.TrackCount);
        Assert.Equal(430, album.Duration);

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(OtherArtistId, tracks.Single(t => t.Id == 1).ArtistId);
        Assert.Equal(OtherArtistId, tracks.Single(t => t.Id == 2).ArtistId);
        Assert.Equal(JazzGenreId, tracks.Single(t => t.Id == 1).GenreId);
        Assert.Equal(JazzGenreId, tracks.Single(t => t.Id == 2).GenreId);
        Assert.Equal(OtherArtistId, tracks.Single(t => t.Id == 4).ArtistId);
        Assert.Equal(OwnGenreId, tracks.Single(t => t.Id == 4).GenreId);

        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE trackId = 1 AND artistId = 2 AND genreId = 2 AND albumId = 1"));
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE trackId = 4 AND artistId = 2 AND genreId = 3 AND albumId = 1"));

        IReadOnlyList<PendingTagWrite> pending = await CreatePendingRepository(fixture).GetByAlbumAsync(AlbumId);
        Assert.Equal(new long[] { 1, 2 }, pending.Select(p => p.TrackId).ToArray());
        Assert.All(pending, p => Assert.Equal(ETagWriteFields.Album | ETagWriteFields.Artist | ETagWriteFields.Year | ETagWriteFields.Genre, p.Fields));

        ArtistEntity? oldArtist = await CreateArtistRepository(fixture).GetByIdAsync(OldArtistId);
        Assert.Equal(0, oldArtist!.TrackCount);
        Assert.Equal(0, oldArtist.TotalDurationSeconds);
        Assert.Equal(1, oldArtist.AlbumCount);
        ArtistEntity? newArtist = await CreateArtistRepository(fixture).GetByIdAsync(OtherArtistId);
        Assert.Equal(4, newArtist!.TrackCount);
        Assert.Equal(670, newArtist.TotalDurationSeconds);
        Assert.Equal(2, newArtist.AlbumCount);
        Assert.Equal(2005, newArtist.YearMini);
        Assert.Equal(2005, newArtist.YearMaxi);

        GenreRepository genres = CreateGenreRepository(fixture);
        GenreEntity? oldGenre = await genres.GetByIdAsync(OldGenreId);
        Assert.Equal(0, oldGenre!.TrackCount);
        Assert.Equal(0, await TotalDurationOfGenreAsync(fixture, OldGenreId));
        Assert.Equal(1, oldGenre.AlbumCount);
        GenreEntity? newGenre = await genres.GetByIdAsync(JazzGenreId);
        Assert.Equal(2, newGenre!.TrackCount);
        Assert.Equal(380, await TotalDurationOfGenreAsync(fixture, JazzGenreId));
        Assert.Equal(2, newGenre.AlbumCount);
    }

    [Fact(DisplayName = "apply_edit_creates_the_missing_artist_and_genre")]
    public async Task ApplyAsync_CreatesTheMissingArtistAndGenre()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumEditRepository repo = CreateRepository(fixture);

        // Act
        AlbumEditApplied applied = await repo.ApplyAsync(BuildChange(newArtistId: null, newArtistName: "New Artist", newGenreId: null, newGenreName: "Fusion"));

        // Assert
        ArtistEntity? artist = await CreateArtistRepository(fixture).GetByIdAsync(applied.ArtistId);
        Assert.NotNull(artist);
        Assert.Equal("New Artist", artist!.Name);
        Assert.Equal(applied.GenreId, artist.GenreId);
        Assert.Equal(2, artist.TrackCount);
        Assert.Equal(380, artist.TotalDurationSeconds);
        Assert.Equal(1, artist.AlbumCount);

        GenreEntity? genre = await CreateGenreRepository(fixture).GetByIdAsync(applied.GenreId!.Value);
        Assert.NotNull(genre);
        Assert.Equal("Fusion", genre!.Name);
        Assert.Equal(2, genre.TrackCount);
        Assert.Equal(1, genre.ArtistCount);
        Assert.Equal(1, genre.AlbumCount);

        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(AlbumId);
        Assert.Equal(applied.ArtistId, album!.ArtistId);
        Assert.Equal(applied.GenreId, album.GenreId);

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(applied.ArtistId, tracks.Single(t => t.Id == 1).ArtistId);
        Assert.Equal(OtherArtistId, tracks.Single(t => t.Id == 4).ArtistId);
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM artists WHERE name = 'New Artist'"));
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genres WHERE name = 'Fusion'"));
    }

    [Fact(DisplayName = "apply_edit_keeps_artist_genre_and_year_when_only_the_name_changes")]
    public async Task ApplyAsync_KeepsArtistGenreAndYearWhenOnlyTheNameChanges()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumEditRepository repo = CreateRepository(fixture);

        // Act
        AlbumEditApplied applied = await repo.ApplyAsync(BuildChange(year: null, newArtistId: null, newGenreId: null, pending: new PendingTagWrite(1, ETagWriteFields.Album)));

        // Assert
        Assert.Equal(new AlbumEditApplied(OldArtistId, OldGenreId), applied);

        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(AlbumId);
        Assert.Equal("Renamed", album!.Name);
        Assert.Equal(OldArtistId, album.ArtistId);
        Assert.Equal(OldGenreId, album.GenreId);
        Assert.Equal(1999, album.Year);

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(OldArtistId, tracks.Single(t => t.Id == 1).ArtistId);
        Assert.Equal(OldGenreId, tracks.Single(t => t.Id == 1).GenreId);
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT trackCount FROM artists WHERE id = 1"));
        Assert.Equal(2, await fixture.Connection.ExecuteScalarAsync<int>("SELECT trackCount FROM genres WHERE id = 1"));
        IReadOnlyList<PendingTagWrite> pending = await CreatePendingRepository(fixture).GetByAlbumAsync(AlbumId);
        Assert.Equal(new PendingTagWrite(1, ETagWriteFields.Album), Assert.Single(pending));
    }

    [Fact(DisplayName = "apply_edit_rolls_back_everything_on_failure")]
    public async Task ApplyAsync_RollsBackEverythingOnFailure()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumEditRepository repo = CreateRepository(fixture);
        AlbumEditDbChange change = BuildChange(
            newArtistId: null,
            newArtistName: "Ghost Artist",
            newGenreId: null,
            newGenreName: "Ghost Genre",
            pending: [new PendingTagWrite(1, ETagWriteFields.Album), new PendingTagWrite(9999, ETagWriteFields.Album)]);

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => repo.ApplyAsync(change));

        // Assert
        AlbumEntity? album = await CreateAlbumRepository(fixture).GetByIdAsync(AlbumId);
        Assert.Equal("The First Album", album!.Name);
        Assert.Equal(OldArtistId, album.ArtistId);
        Assert.Equal(OldGenreId, album.GenreId);
        Assert.Equal(1999, album.Year);
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM artists WHERE name = 'Ghost Artist'"));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM genres WHERE name = 'Ghost Genre'"));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pendingTagWrites"));

        List<TrackEntity> tracks = (await CreateTrackRepository(fixture).GetAllAsync()).ToList();
        Assert.Equal(OldArtistId, tracks.Single(t => t.Id == 1).ArtistId);
        Assert.Equal(OldGenreId, tracks.Single(t => t.Id == 1).GenreId);
        Assert.Equal(1, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ListeningEvents WHERE trackId = 1 AND artistId = 1 AND genreId = 1"));
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT trackCount FROM artists WHERE id = 1"));
    }

    [Fact(DisplayName = "apply_edit_of_an_unknown_album_creates_nothing")]
    public async Task ApplyAsync_UnknownAlbum_CreatesNothing()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        await SeedAsync(fixture);
        AlbumEditRepository repo = CreateRepository(fixture);
        AlbumEditDbChange change = BuildChange(newArtistId: null, newArtistName: "Ghost Artist", pending: []) with { AlbumId = 999 };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyAsync(change));

        // Assert
        Assert.Equal(0, await fixture.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM artists WHERE name = 'Ghost Artist'"));
    }

    [Fact(DisplayName = "apply_edit_rejects_a_blank_name_or_a_non_positive_id")]
    public async Task ApplyAsync_InvalidChange_Throws()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        AlbumEditRepository repo = CreateRepository(fixture);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.ApplyAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.ApplyAsync(BuildChange() with { AlbumId = 0 }));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ApplyAsync(BuildChange(name: " ")));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.ApplyAsync(BuildChange(newArtistId: null, newArtistName: " ")));
    }
}