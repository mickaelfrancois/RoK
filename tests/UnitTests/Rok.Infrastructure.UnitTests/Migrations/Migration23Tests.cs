using Dapper;
using Microsoft.Data.Sqlite;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migrations;

public class Migration23Tests
{
    private const string Old = "2020-01-01";
    private const string Recent = "2024-01-01";

    private static void SimulateVersion22(SqliteDatabaseFixture fixture)
    {
        fixture.Connection.Execute("DROP INDEX IF EXISTS Artists_name");
        fixture.Connection.Execute("CREATE UNIQUE INDEX Artists_name ON Artists(name)");
        fixture.Connection.Execute("DROP INDEX IF EXISTS Genres_name");
        fixture.Connection.Execute("CREATE UNIQUE INDEX Genres_name ON Genres(name)");
    }

    private static void InsertArtist(SqliteDatabaseFixture fixture, long id, string name, string date, int favorite = 0, int listens = 0, string? lastListen = null, string? biography = null, long? genreId = null)
    {
        fixture.Connection.Execute(
            "INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, lastListen, biography, genreId, creatDate) VALUES (@id, @name, 0, 0, 0, 0, 0, 0, 0, @favorite, @listens, @lastListen, @biography, @genreId, @date)",
            new { id, name, date, favorite, listens, lastListen, biography, genreId });
    }

    private static void InsertGenre(SqliteDatabaseFixture fixture, long id, string name, string date, int favorite = 0, int listens = 0)
    {
        fixture.Connection.Execute(
            "INSERT INTO Genres(id, name, totalDurationSeconds, trackCount, artistCount, compilationCount, bestofCount, albumCount, liveCount, listenCount, isFavorite, creatDate) VALUES (@id, @name, 0, 0, 0, 0, 0, 0, 0, @listens, @favorite, @date)",
            new { id, name, date, favorite, listens });
    }

    private static void SeedDuplicates(SqliteDatabaseFixture fixture)
    {
        InsertArtist(fixture, 100, "AC/DC", Old, favorite: 0, listens: 3, lastListen: "2023-01-01");
        InsertArtist(fixture, 101, "Ac/Dc", Recent, favorite: 1, listens: 4, lastListen: "2024-06-01", biography: "bio");
        InsertGenre(fixture, 100, "Metal", Old, favorite: 0, listens: 2);
        InsertGenre(fixture, 101, "metal", Recent, favorite: 1, listens: 5);
        fixture.Connection.Execute("UPDATE Artists SET genreId = 101 WHERE id = 101");

        fixture.Connection.Execute("INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId) VALUES (100, 'Back in Black', 0, 0, 0, 0, 0, 0, 0, @d, 101, 101)", new { d = Recent });
        fixture.Connection.Execute("INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, genreId, trackNumber) VALUES (100, 'Hells Bells', 100, 1, 128, '/hb', @d, 0, 0, 0, 0, @d, 100, 101, 101, 1)", new { d = Recent });
        fixture.Connection.Execute("INSERT INTO ListeningEvents(trackId, artistId, albumId, genreId, playedAt, wasSkipped, durationPlayed, durationTotal) VALUES (100, 101, 100, 101, @d, 0, 10, 100)", new { d = Recent });

        fixture.Connection.Execute("INSERT INTO Tags(id, name) VALUES (100, 'hard'), (101, 'loud')");
        fixture.Connection.Execute("INSERT INTO ArtistTags(artistId, tagId) VALUES (100, 100), (101, 100), (101, 101)");
        fixture.Connection.Execute("INSERT INTO EqualizerPreset(Scope, ScopeId) VALUES ('Artist', 101), ('Genre', 100), ('Genre', 101)");
    }

    [Fact(DisplayName = "migration_23_merges_duplicate_artists_keeping_oldest")]
    public void Migration23_DuplicateArtists_KeepsOldest()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Equal([100L], fixture.Connection.Query<long>("SELECT id FROM Artists WHERE name LIKE 'ac/dc'").ToList());
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT artistId FROM Tracks WHERE id = 100"));
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT artistId FROM Albums WHERE id = 100"));
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT artistId FROM ListeningEvents WHERE trackId = 100"));
        Assert.Equal("AC/DC", fixture.Connection.ExecuteScalar<string>("SELECT name FROM Artists WHERE id = 100"));
    }

    [Fact(DisplayName = "migration_23_merges_user_data")]
    public void Migration23_DuplicateArtists_MergesUserData()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        var artist = fixture.Connection.QuerySingle<(int IsFavorite, int ListenCount, string LastListen, string Biography)>("SELECT isFavorite, listenCount, lastListen, biography FROM Artists WHERE id = 100");
        Assert.Equal((1, 7, "2024-06-01", "bio"), artist);
        Assert.Equal([100L, 101L], fixture.Connection.Query<long>("SELECT tagId FROM ArtistTags WHERE artistId = 100 ORDER BY tagId").ToList());
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ArtistTags WHERE artistId = 101"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Artist' AND ScopeId = 100"));
    }

    [Fact(DisplayName = "migration_23_merges_duplicate_genres")]
    public void Migration23_DuplicateGenres_MergesAndReattaches()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Equal([100L], fixture.Connection.Query<long>("SELECT id FROM Genres WHERE name LIKE 'metal'").ToList());
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT genreId FROM Tracks WHERE id = 100"));
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT genreId FROM Albums WHERE id = 100"));
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT genreId FROM Artists WHERE id = 100"));
        Assert.Equal(100L, fixture.Connection.ExecuteScalar<long>("SELECT genreId FROM ListeningEvents WHERE trackId = 100"));
        Assert.Equal((1, 7), fixture.Connection.QuerySingle<(int, int)>("SELECT isFavorite, listenCount FROM Genres WHERE id = 100"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM EqualizerPreset WHERE Scope = 'Genre'"));
    }

    [Fact(DisplayName = "migration_23_recomputes_counters")]
    public void Migration23_DuplicateArtists_RecomputesCounters()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Equal((1, 100), fixture.Connection.QuerySingle<(int, int)>("SELECT trackCount, totalDurationSeconds FROM Artists WHERE id = 100"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT trackCount FROM Albums WHERE id = 100"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT trackCount FROM Genres WHERE id = 100"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT artistCount FROM Genres WHERE id = 100"));
    }

    [Fact(DisplayName = "migration_23_without_duplicates_changes_nothing")]
    public void Migration23_NoDuplicates_ChangesOnlyIndexes()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        const string artists = "SELECT id || '|' || name || '|' || trackCount || '|' || listenCount FROM Artists ORDER BY id";
        const string genres = "SELECT id || '|' || name || '|' || trackCount || '|' || listenCount FROM Genres ORDER BY id";
        List<string> artistsBefore = fixture.Connection.Query<string>(artists).ToList();
        List<string> genresBefore = fixture.Connection.Query<string>(genres).ToList();

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Equal(artistsBefore, fixture.Connection.Query<string>(artists).ToList());
        Assert.Equal(genresBefore, fixture.Connection.Query<string>(genres).ToList());
        Assert.Contains("COLLATE NOCASE", fixture.Connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name = 'Artists_name'"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "artist_unique_index_rejects_case_duplicate")]
    public void Migration23_ArtistIndex_RejectsCaseDuplicate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        Action act = () => InsertArtist(fixture, 200, "ARTIST a", Recent);

        // Assert
        Assert.Throws<SqliteException>(act);
    }

    [Fact(DisplayName = "genre_unique_index_rejects_case_duplicate")]
    public void Migration23_GenreIndex_RejectsCaseDuplicate()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();

        // Act
        Action act = () => InsertGenre(fixture, 200, "ROCK", Recent);

        // Assert
        Assert.Throws<SqliteException>(act);
    }

    [Fact(DisplayName = "migration_23_is_idempotent")]
    public void Migration23_AppliedTwice_KeepsData()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);
        new Migration23().Apply(fixture.Connection);

        // Act
        new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Artists WHERE name LIKE 'ac/dc'"));
        Assert.Equal(7, fixture.Connection.ExecuteScalar<int>("SELECT listenCount FROM Artists WHERE id = 100"));
    }

    [Fact(DisplayName = "migration_23_rolls_back_on_failure")]
    public void Migration23_FailureMidWay_ChangesNothing()
    {
        // Arrange
        using SqliteDatabaseFixture fixture = new();
        SimulateVersion22(fixture);
        SeedDuplicates(fixture);
        fixture.Connection.Execute("CREATE TRIGGER fail_artist_delete BEFORE DELETE ON Artists BEGIN SELECT RAISE(ABORT, 'forced failure'); END");
        const string artists = "SELECT id || '|' || name || '|' || listenCount || '|' || trackCount FROM Artists ORDER BY id";
        const string tracks = "SELECT id || '|' || artistId || '|' || genreId FROM Tracks ORDER BY id";
        List<string> artistsBefore = fixture.Connection.Query<string>(artists).ToList();
        List<string> tracksBefore = fixture.Connection.Query<string>(tracks).ToList();
        int genresBefore = fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Genres");

        // Act
        Action act = () => new Migration23().Apply(fixture.Connection);

        // Assert
        Assert.Throws<SqliteException>(act);
        Assert.Equal(artistsBefore, fixture.Connection.Query<string>(artists).ToList());
        Assert.Equal(tracksBefore, fixture.Connection.Query<string>(tracks).ToList());
        Assert.Equal(genresBefore, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Genres"));
        Assert.DoesNotContain("NOCASE", fixture.Connection.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name = 'Artists_name'"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "migration_23_targets_version_23")]
    public void Migration23_TargetVersion_Is23()
    {
        // Act
        var version = new Migration23().TargetVersion;

        // Assert
        Assert.Equal(23, version);
    }
}