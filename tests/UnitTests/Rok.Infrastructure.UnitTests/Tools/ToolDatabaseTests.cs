using Dapper;
using Microsoft.Data.Sqlite;
using Rok.MetadataTool;

namespace Rok.Infrastructure.UnitTests.Tools;

public sealed class ToolDatabaseTests : IDisposable
{
    private readonly TempRokDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact(DisplayName = "table_exists_reports_existing_and_missing_tables")]
    public void TableExists_ReportsExistingAndMissingTables()
    {
        string connectionString = ToolDatabase.ReadOnlyConnectionString(_database.DatabasePath);

        Assert.True(ToolDatabase.TableExists(connectionString, "Tracks"));
        Assert.False(ToolDatabase.TableExists(connectionString, "NoSuchTable"));
    }

    [Fact(DisplayName = "column_exists_reports_existing_and_missing_columns")]
    public void ColumnExists_ReportsExistingAndMissingColumns()
    {
        string connectionString = ToolDatabase.ReadOnlyConnectionString(_database.DatabasePath);

        Assert.True(ToolDatabase.ColumnExists(connectionString, "trackAnalysis", "outroMixPointScore"));
        Assert.False(ToolDatabase.ColumnExists(connectionString, "Tracks", "noSuchColumn"));
    }

    [Fact(DisplayName = "read_only_connection_string_rejects_writes")]
    public void ReadOnlyConnectionString_RejectsWrites()
    {
        using SqliteConnection connection = new(ToolDatabase.ReadOnlyConnectionString(_database.DatabasePath));
        connection.Open();

        Assert.Throws<SqliteException>(() => connection.Execute("UPDATE Tracks SET title = 'x';"));
    }

    [Fact(DisplayName = "analysis_write_connection_string_is_private_cache_with_short_timeout")]
    public void AnalysisWriteConnectionString_IsPrivateCacheWithShortTimeout()
    {
        SqliteConnectionStringBuilder builder = new(ToolDatabase.AnalysisWriteConnectionString(_database.DatabasePath));

        Assert.Equal(SqliteOpenMode.ReadWrite, builder.Mode);
        Assert.Equal(SqliteCacheMode.Private, builder.Cache);
        Assert.False(builder.Pooling);
        Assert.Equal(5, builder.DefaultTimeout);
    }

    [Fact(DisplayName = "backup_copies_a_consistent_image_including_wal_pages")]
    public void CreateBackup_CopiesConsistentImageIncludingWalPages()
    {
        _database.Checkpoint();

        // Held open so the rows below stay in the -wal file instead of being checkpointed away.
        using SqliteConnection writer = _database.Open();
        writer.Execute("PRAGMA wal_autocheckpoint = 0;");
        writer.Execute("UPDATE Tracks SET title = 'only in wal' WHERE id = 1;");

        string backupPath = ToolDatabase.CreateBackup(_database.DatabasePath, new DateTime(2026, 10, 3, 14, 5, 6));

        Assert.EndsWith(".20261003_140506.bak", backupPath);
        Assert.True(File.Exists(backupPath));

        using SqliteConnection backup = new(ToolDatabase.ReadOnlyConnectionString(backupPath));
        backup.Open();
        Assert.Equal("only in wal", backup.ExecuteScalar<string>("SELECT title FROM Tracks WHERE id = 1;"));
    }
}