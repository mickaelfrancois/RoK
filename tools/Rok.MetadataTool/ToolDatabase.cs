using Microsoft.Data.Sqlite;

namespace Rok.MetadataTool;

/// <summary>SQLite helpers shared by the tool commands.</summary>
internal static class ToolDatabase
{
    public static bool TableExists(string connectionString, string table)
    {
        using SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    public static bool ColumnExists(string connectionString, string table, string column)
    {
        using SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);

        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Writes a consistent copy of the database next to it and returns the path of the backup.
    /// </summary>
    public static string CreateBackup(string databasePath, DateTime now)
    {
        string backupPath = $"{databasePath}.{now:yyyyMMdd_HHmmss}.bak";

        using SqliteConnection source = new(ReadOnlyConnectionString(databasePath));
        source.Open();

        using SqliteConnection destination = new(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString());
        destination.Open();

        // SQLite online-backup API copies a consistent image including pending WAL pages,
        // unlike a plain File.Copy of the main database file.
        source.BackupDatabase(destination);

        return backupPath;
    }

    public static string ReadOnlyConnectionString(string databasePath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
    }

    /// <summary>
    /// Read-only connection that never creates the <c>-wal</c> / <c>-shm</c> side files (SQLite <c>immutable=1</c>).
    /// Only safe when nothing else uses the database and no WAL content is pending: see <see cref="CanOpenImmutable"/>.
    /// </summary>
    public static string ImmutableReadOnlyConnectionString(string databasePath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = $"{new Uri(Path.GetFullPath(databasePath)).AbsoluteUri}?mode=ro&immutable=1",
            Pooling = false
        }.ToString();
    }

    /// <summary>True when no WAL content is pending, so an immutable read sees the whole database.</summary>
    public static bool CanOpenImmutable(string databasePath)
    {
        FileInfo wal = new($"{databasePath}-wal");

        return !wal.Exists || wal.Length == 0;
    }

    public static string AnalysisWriteConnectionString(string databasePath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
    }
}