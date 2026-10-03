using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Tools;

/// <summary>A migrated, WAL-mode Rok database stored in a temporary file, never the developer's real one.</summary>
internal sealed class TempRokDatabase : IDisposable
{
    private readonly string _directory;

    public TempRokDatabase()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"rok-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        DatabasePath = Path.Combine(_directory, "database.sqlite");

        using SqliteConnection connection = Open();

        var migrations = new IMigration[] { new Migration2(), new Migration3(), new Migration4(), new Migration5(), new Migration6(), new Migration7(), new Migration8(), new Migration9(), new Migration10(), new Migration11(), new Migration12(), new Migration13(), new Migration14(), new Migration15(), new Migration16(), new Migration17(), new Migration18(), new Migration19() };
        MigrationService migrationService = new(connection, migrations, NullLogger<MigrationService>.Instance);
        migrationService.Initial();
        migrationService.MigrateToLatest();

        Seed(connection);
    }

    public string DatabasePath { get; }

    public string DirectoryPath => _directory;

    public SqliteConnection Open()
    {
        SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();

        return connection;
    }

    public void Checkpoint()
    {
        using SqliteConnection connection = Open();
        connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
    }

    public string Sha256()
    {
        using FileStream stream = new(DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public long CountAnalysisRows()
    {
        using SqliteConnection connection = Open();

        return connection.ExecuteScalar<long>("SELECT count(*) FROM trackAnalysis;");
    }

    private static void Seed(SqliteConnection connection)
    {
        DateTime now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        connection.Execute("INSERT INTO Genres(id, name, totalDurationSeconds, trackCount, artistCount, compilationCount, bestofCount, albumCount, liveCount, listenCount, isFavorite, creatDate) VALUES (1, 'Rock', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)", new { now });
        connection.Execute("INSERT INTO Artists(id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds, disbanded, isFavorite, listenCount, creatDate) VALUES (1, 'Artist A', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)", new { now });
        connection.Execute(@"INSERT INTO Albums(id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId) VALUES
            (1, 'Studio Album', 0, 0, 0, 3, 600, 0, 0, @now, 1, 1),
            (2, 'Live Album', 1, 0, 0, 1, 200, 0, 0, @now, 1, 1)", new { now });
        connection.Execute(@"INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber) VALUES
            (1, 'studio 1', 180, 1000, 128, 'C:\music\t1.mp3', @now, 0, 0, 0, 0, @now, 1, 1, 1),
            (2, 'studio 2', 200, 1200, 128, 'C:\music\t2.mp3', @now, 0, 0, 0, 0, @now, 1, 1, 2),
            (3, 'live track', 220, 1400, 128, 'C:\music\t3.mp3', @now, 1, 0, 0, 0, @now, 1, 1, 3),
            (4, 'on live album', 200, 1600, 128, 'C:\music\t4.mp3', @now, 0, 0, 0, 0, @now, 2, 1, 1)", new { now });
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
    }
}