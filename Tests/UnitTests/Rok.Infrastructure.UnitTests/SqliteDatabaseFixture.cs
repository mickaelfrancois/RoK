using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Infrastructure.Migration;
using System.Data;

namespace Rok.Infrastructure.UnitTests;

public class SqliteDatabaseFixture : IDisposable
{
    public IDbConnection Connection { get; }
    private readonly SqliteConnection _sqliteConnection;

    public SqliteDatabaseFixture()
    {
        string connectionString = $"Data Source=InMemoryDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _sqliteConnection = new SqliteConnection(connectionString);
        _sqliteConnection.Open();
        Connection = _sqliteConnection;

        IMigration[] migrations = new IMigration[] { new Migration2() };
        MigrationService migrationService = new(Connection, migrations, NullLogger<MigrationService>.Instance);
        migrationService.Initial();
        migrationService.MigrateToLatest();

        SeedData();
    }

    private void SeedData()
    {
        DateTime now = DateTime.UtcNow;

        Connection.Execute(
            "INSERT INTO Countries(id, code, creatDate) VALUES (@id, @code, @creatDate)",
            new { id = 1, code = "FR", creatDate = now });

        Connection.Execute(@"
            INSERT INTO Genres(
                id, name, totalDurationSeconds, trackCount, artistCount, compilationCount, bestofCount, albumCount,
                liveCount, listenCount, isFavorite, creatDate
            ) VALUES
            (1, 'Rock', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now),
            (2, 'Jazz', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)
            ", new { now });

        Connection.Execute(@"
            INSERT INTO Artists(
                id, name, trackCount, albumCount, liveCount, compilationCount, bestofCount, totalDurationSeconds,
                disbanded, isFavorite, listenCount, creatDate
            ) VALUES
            (1, 'Artist A', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now),
            (2, 'Artist B', 0, 0, 0, 0, 0, 0, 0, 0, 0, @now)
            ", new { now });

        Connection.Execute(@"
            INSERT INTO Albums(
                id, name, isLive, isCompilation, isBestof, trackCount, duration, isFavorite, listenCount, creatDate, artistId, genreId
            ) VALUES
            (1, 'The First Album', 0, 0, 0, 10, 3600, 0, 0, @now, 1, 1),
            (2, 'Second Sounds', 0, 0, 0, 8, 2400, 1, 5, @now, 1, 2),
            (3, 'Another Album', 0, 0, 0, 9, 3000, 0, 1, @now, 2, 1)
            ", new { now });

        Connection.Execute(@"
            INSERT INTO Tracks(
                id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, albumId, artistId, trackNumber
            ) VALUES
            (1, 't1', 180, 1000, 128, '/f1', @now, 0, 0, 0, 0, @now, 1, 1, 2),
            (2, 't2', 200, 1200, 128, '/f2', @now, 0, 0, 0, 0, @now, 1, 1, 1),
            (3, 't3', 240, 1500, 192, '/f3', @now, 0, 0, 0, 0, @now, 2, 2, 3)
            ", new { now });
    }

    public void Dispose()
    {
        Connection?.Dispose();
    }
}
