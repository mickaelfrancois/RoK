using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests.Migration;

public class MigrationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public MigrationServiceTests()
    {
        string connectionString = $"Data Source=InMemoryDb_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
    }

    [Fact]
    public void Initial_CreatesAllTablesAndSetsVersion()
    {
        // Arrange
        MigrationService sut = new(_connection, null, NullLogger<MigrationService>.Instance);

        // Act
        sut.Initial();

        // Assert
        Assert.Equal(1, sut.GetDatabaseVersion());
        AssertTableExists("Genres");
        AssertTableExists("Artists");
        AssertTableExists("Albums");
        AssertTableExists("Tracks");
        AssertTableExists("Countries");
        AssertTableExists("Playlists");
        AssertTableExists("PlaylistTracks");
    }

    [Fact]
    public void GetDatabaseVersion_OnEmptyDatabase_ReturnsZero()
    {
        // Arrange
        MigrationService sut = new(_connection, null, NullLogger<MigrationService>.Instance);

        // Act
        int version = sut.GetDatabaseVersion();

        // Assert
        Assert.Equal(0, version);
    }

    [Fact]
    public void GetDatabaseVersion_AfterInitial_ReturnsOne()
    {
        // Arrange
        MigrationService sut = new(_connection, null, NullLogger<MigrationService>.Instance);
        sut.Initial();

        // Act
        int version = sut.GetDatabaseVersion();

        // Assert
        Assert.Equal(1, version);
    }

    [Fact]
    public void MigrateToLatest_WithNoMigrations_DoesNothing()
    {
        // Arrange
        MigrationService sut = new(_connection, null, NullLogger<MigrationService>.Instance);
        sut.Initial();

        // Act
        sut.MigrateToLatest();

        // Assert
        Assert.Equal(1, sut.GetDatabaseVersion());
    }

    [Fact]
    public void MigrateToLatest_WithOneMigration_AppliesMigration()
    {
        // Arrange
        Mock<IMigration> migration = new();
        migration.Setup(m => m.TargetVersion).Returns(2);
        migration.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        MigrationService sut = new(_connection, new[] { migration.Object }, NullLogger<MigrationService>.Instance);
        sut.Initial();

        // Act
        sut.MigrateToLatest();

        // Assert
        Assert.Equal(2, sut.GetDatabaseVersion());
        migration.Verify(m => m.Apply(_connection), Times.Once);
    }

    [Fact]
    public void MigrateToLatest_WithMultipleMigrations_AppliesInOrder()
    {
        // Arrange
        Mock<IMigration> migration2 = new();
        migration2.Setup(m => m.TargetVersion).Returns(2);
        migration2.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        Mock<IMigration> migration3 = new();
        migration3.Setup(m => m.TargetVersion).Returns(3);
        migration3.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        Mock<IMigration> migration4 = new();
        migration4.Setup(m => m.TargetVersion).Returns(4);
        migration4.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        IMigration[] migrations = new[] { migration4.Object, migration2.Object, migration3.Object };
        MigrationService sut = new(_connection, migrations, NullLogger<MigrationService>.Instance);
        sut.Initial();

        // Act
        sut.MigrateToLatest();

        // Assert
        Assert.Equal(4, sut.GetDatabaseVersion());
        migration2.Verify(m => m.Apply(_connection), Times.Once);
        migration3.Verify(m => m.Apply(_connection), Times.Once);
        migration4.Verify(m => m.Apply(_connection), Times.Once);
    }

    [Fact]
    public void MigrateToLatest_OnlyAppliesPendingMigrations()
    {
        // Arrange
        Mock<IMigration> migration2 = new();
        migration2.Setup(m => m.TargetVersion).Returns(2);
        migration2.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        Mock<IMigration> migration3 = new();
        migration3.Setup(m => m.TargetVersion).Returns(3);
        migration3.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        IMigration[] migrations = new[] { migration2.Object, migration3.Object };
        MigrationService sut = new(_connection, migrations, NullLogger<MigrationService>.Instance);
        sut.Initial();
        sut.MigrateToLatest();

        migration2.Invocations.Clear();
        migration3.Invocations.Clear();

        Mock<IMigration> migration4 = new();
        migration4.Setup(m => m.TargetVersion).Returns(4);
        migration4.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        MigrationService sut2 = new(_connection, new[] { migration2.Object, migration3.Object, migration4.Object }, NullLogger<MigrationService>.Instance);

        // Act
        sut2.MigrateToLatest();

        // Assert
        Assert.Equal(4, sut2.GetDatabaseVersion());
        migration2.Verify(m => m.Apply(_connection), Times.Never);
        migration3.Verify(m => m.Apply(_connection), Times.Never);
        migration4.Verify(m => m.Apply(_connection), Times.Once);
    }

    [Fact]
    public void MigrateToLatest_WithRealMigration2_AppliesSuccessfully()
    {
        // Arrange
        IMigration[] migrations = new IMigration[] { new Migration2() };
        MigrationService sut = new(_connection, migrations, NullLogger<MigrationService>.Instance);
        sut.Initial();

        _connection.Execute("INSERT INTO Tracks(id, title, duration, size, bitrate, musicFile, fileDate, isLive, score, listenCount, skipCount, creatDate, trackNumber) VALUES (1, 'Test', 180, 1000, 128, '/test.mp3', @now, 0, 0, 0, 0, @now, 1)", new { now = DateTime.UtcNow });

        // Act
        sut.MigrateToLatest();

        // Assert
        Assert.Equal(2, sut.GetDatabaseVersion());
        AssertColumnExists("Tracks", "getLyricsLastAttempt");
        AssertColumnExists("Artists", "getMetaDataLastAttempt");
        AssertColumnExists("Albums", "getMetaDataLastAttempt");
    }

    [Fact]
    public void MigrateToLatest_WhenMigrationFails_ThrowsException()
    {
        // Arrange
        Mock<IMigration> failingMigration = new();
        failingMigration.Setup(m => m.TargetVersion).Returns(2);
        failingMigration.Setup(m => m.Apply(It.IsAny<IDbConnection>())).Throws(new InvalidOperationException("Migration failed"));

        Mock<ILogger<MigrationService>> logger = new();
        MigrationService sut = new(_connection, new[] { failingMigration.Object }, logger.Object);
        sut.Initial();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sut.MigrateToLatest());
        Assert.Equal(1, sut.GetDatabaseVersion());
    }

    [Fact]
    public void MigrateToLatest_WhenMigrationFails_LogsCriticalError()
    {
        // Arrange
        Mock<IMigration> failingMigration = new();
        failingMigration.Setup(m => m.TargetVersion).Returns(2);
        failingMigration.Setup(m => m.Apply(It.IsAny<IDbConnection>())).Throws(new InvalidOperationException("Migration failed"));

        Mock<ILogger<MigrationService>> logger = new();
        MigrationService sut = new(_connection, new[] { failingMigration.Object }, logger.Object);
        sut.Initial();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sut.MigrateToLatest());
        logger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CompleteFlow_InitialThenMigrations_ReachesCorrectVersion()
    {
        // Arrange
        Mock<IMigration> migration2 = new();
        migration2.Setup(m => m.TargetVersion).Returns(2);
        migration2.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        Mock<IMigration> migration3 = new();
        migration3.Setup(m => m.TargetVersion).Returns(3);
        migration3.Setup(m => m.Apply(It.IsAny<IDbConnection>()));

        IMigration[] migrations = new[] { migration2.Object, migration3.Object };
        MigrationService sut = new(_connection, migrations, NullLogger<MigrationService>.Instance);

        // Act
        sut.Initial();
        int versionAfterInitial = sut.GetDatabaseVersion();

        sut.MigrateToLatest();
        int versionAfterMigration = sut.GetDatabaseVersion();

        // Assert
        Assert.Equal(1, versionAfterInitial);
        Assert.Equal(3, versionAfterMigration);
        migration2.Verify(m => m.Apply(_connection), Times.Once);
        migration3.Verify(m => m.Apply(_connection), Times.Once);
    }

    private void AssertTableExists(string tableName)
    {
        string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        int count = _connection.ExecuteScalar<int>(sql, new { name = tableName });
        Assert.Equal(1, count);
    }

    private void AssertColumnExists(string tableName, string columnName)
    {
        string sql = $"PRAGMA table_info({tableName})";
        IEnumerable<dynamic> columns = _connection.Query(sql);
        Assert.Contains(columns, c => ((string)c.name).Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connection?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}