using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Lyrics;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Tag;
using Rok.Import.Services;

if (args.Length < 1 || args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return args.Length < 1 ? 1 : 0;
}

string databasePath = args[0];
bool apply = args.Contains("--apply");

if (!File.Exists(databasePath))
{
    Console.Error.WriteLine($"Database not found: {databasePath}");
    return 1;
}

string connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath,
    Mode = SqliteOpenMode.ReadWrite,
    Cache = SqliteCacheMode.Shared,
    Pooling = false
}.ToString();

Console.WriteLine($"Database : {databasePath}");
Console.WriteLine($"Mode     : {(apply ? "APPLY (database will be modified)" : "dry-run (no changes written)")}");
Console.WriteLine();

try
{
    if (!TableExists(connectionString, "Tracks"))
    {
        Console.Error.WriteLine("This file does not look like a Rok database (no 'Tracks' table).");
        return 1;
    }

    // The tool does not migrate; it expects an up-to-date schema (open the database once in Rok first).
    if (!ColumnExists(connectionString, "Tracks", "disc"))
    {
        Console.Error.WriteLine("Database schema is out of date for this tool. Open the database once in Rok (which applies migrations), then retry.");
        return 1;
    }

    ServiceCollection services = new();
    services.AddLogging();
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IDbConnection>(_ => new SqliteConnection(connectionString));
    services.AddKeyedSingleton<IDbConnection>("BackgroundConnection", (_, _) => new SqliteConnection(connectionString));
    services.AddSingleton<IFileSystem, DefaultFileSystem>();
    services.AddSingleton<ILyricsService, LyricsService>();
    services.AddSingleton<ITagService, TagService>();
    services.AddSingleton<ITrackRepository, TrackRepository>();
    services.AddSingleton<IAlbumRepository, AlbumRepository>();
    services.AddSingleton<EmbeddedLyricsImporter>();
    services.AddSingleton<LibraryMetadataRefresher>();

    using ServiceProvider provider = services.BuildServiceProvider();

    if (apply)
    {
        string backupPath = $"{databasePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        BackupDatabase(databasePath, backupPath);
        Console.WriteLine($"Backup created: {backupPath}");
        Console.WriteLine();
    }

    LibraryMetadataRefresher refresher = provider.GetRequiredService<LibraryMetadataRefresher>();
    LibraryMetadataRefreshReport report = await refresher.RefreshAsync(apply);

    PrintReport(report);
}
catch (SqliteException ex)
{
    Console.Error.WriteLine($"SQLite error: {ex.Message}");
    Console.Error.WriteLine("Make sure the Rok application is closed (the database must not be locked).");
    return 1;
}

return 0;

static bool TableExists(string connectionString, string table)
{
    using SqliteConnection connection = new(connectionString);
    connection.Open();

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
    command.Parameters.AddWithValue("$name", table);

    return Convert.ToInt64(command.ExecuteScalar()) > 0;
}

static bool ColumnExists(string connectionString, string table, string column)
{
    using SqliteConnection connection = new(connectionString);
    connection.Open();

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM pragma_table_info($table) WHERE name = $column;";
    command.Parameters.AddWithValue("$table", table);
    command.Parameters.AddWithValue("$column", column);

    return Convert.ToInt64(command.ExecuteScalar()) > 0;
}

static void BackupDatabase(string databasePath, string backupPath)
{
    using SqliteConnection source = new(new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadOnly,
        Pooling = false
    }.ToString());
    source.Open();

    using SqliteConnection destination = new(new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString());
    destination.Open();

    // SQLite online-backup API copies a consistent image including pending WAL pages,
    // unlike a plain File.Copy of the main database file.
    source.BackupDatabase(destination);
}

static void PrintUsage()
{
    Console.WriteLine("Usage: Rok.MetadataTool <database.sqlite> [--apply]");
    Console.WriteLine();
    Console.WriteLine("  Re-reads the audio tag of every track in the database and refreshes the");
    Console.WriteLine("  extended metadata columns (disc, bpm, composers, audio specs, replaygain),");
    Console.WriteLine("  the album MusicBrainz id and the embedded-lyrics sidecars.");
    Console.WriteLine();
    Console.WriteLine("  A value is written only when the tag provides one; existing values are");
    Console.WriteLine("  never blanked. The tool does not migrate the schema: open the database once");
    Console.WriteLine("  in Rok first. Without --apply it runs as a dry-run and writes nothing.");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <database.sqlite>  Path to an up-to-date Rok SQLite database.");
    Console.WriteLine("  --apply            Persist the changes (a timestamped .bak backup is taken first).");
}

static void PrintReport(LibraryMetadataRefreshReport report)
{
    Console.WriteLine(report.Applied ? "Changes applied:" : "Changes that would be applied (dry-run):");
    Console.WriteLine($"  Tracks scanned          : {report.TracksScanned}");
    Console.WriteLine($"  Files missing on disk   : {report.FilesMissing}");
    Console.WriteLine($"  Tracks {(report.Applied ? "updated" : "to update")}          : {report.TracksUpdated}");
    Console.WriteLine($"  Albums {(report.Applied ? "updated" : "to update")}          : {report.AlbumsUpdated}");
    Console.WriteLine($"  Lyrics sidecars {(report.Applied ? "created" : "to create")} : {report.LyricsSidecarsCreated}");

    if (report.LyricsSidecarsFailed > 0)
        Console.WriteLine($"  Lyrics sidecars failed  : {report.LyricsSidecarsFailed}");

    if (!report.Applied)
    {
        Console.WriteLine();
        Console.WriteLine("Run again with --apply to persist these changes.");
    }
}
