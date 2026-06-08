using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Tag;
using Rok.Infrastructure;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Lyrics;
using Rok.Infrastructure.Migration;
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

services.AddSingleton<IMigrationService, MigrationService>();
services.AddSingleton<IMigration, Migration2>();
services.AddSingleton<IMigration, Migration3>();
services.AddSingleton<IMigration, Migration4>();
services.AddSingleton<IMigration, Migration5>();
services.AddSingleton<IMigration, Migration6>();
services.AddSingleton<IMigration, Migration7>();
services.AddSingleton<IMigration, Migration8>();
services.AddSingleton<IMigration, Migration9>();
services.AddSingleton<IMigration, Migration10>();
services.AddSingleton<IMigration, Migration11>();
services.AddSingleton<IMigration, Migration12>();
services.AddSingleton<IMigration, Migration13>();
services.AddSingleton<IMigration, Migration14>();

using ServiceProvider provider = services.BuildServiceProvider();

Console.WriteLine($"Database : {databasePath}");
Console.WriteLine($"Mode     : {(apply ? "APPLY (database will be modified)" : "dry-run (no changes written)")}");
Console.WriteLine();

try
{
    IMigrationService migrationService = provider.GetRequiredService<IMigrationService>();
    migrationService.MigrateToLatest();

    if (apply)
    {
        string backupPath = $"{databasePath}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        File.Copy(databasePath, backupPath, overwrite: false);
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

static void PrintUsage()
{
    Console.WriteLine("Usage: Rok.MetadataTool <database.sqlite> [--apply]");
    Console.WriteLine();
    Console.WriteLine("  Re-reads the audio tag of every track in the database and refreshes the");
    Console.WriteLine("  extended metadata columns (disc, bpm, composers, audio specs, replaygain),");
    Console.WriteLine("  the album MusicBrainz id and the embedded-lyrics sidecars.");
    Console.WriteLine();
    Console.WriteLine("  A value is written only when the tag provides one; existing values are");
    Console.WriteLine("  never blanked. Without --apply the tool runs as a dry-run.");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <database.sqlite>  Path to the Rok SQLite database.");
    Console.WriteLine("  --apply            Persist the changes (a timestamped .bak backup is created first).");
}

static void PrintReport(LibraryMetadataRefreshReport report)
{
    Console.WriteLine(report.Applied ? "Changes applied:" : "Changes that would be applied (dry-run):");
    Console.WriteLine($"  Tracks scanned          : {report.TracksScanned}");
    Console.WriteLine($"  Files missing on disk   : {report.FilesMissing}");
    Console.WriteLine($"  Tracks {(report.Applied ? "updated" : "to update")}          : {report.TracksUpdated}");
    Console.WriteLine($"  Albums {(report.Applied ? "updated" : "to update")}          : {report.AlbumsUpdated}");
    Console.WriteLine($"  Lyrics sidecars {(report.Applied ? "created" : "to create")} : {report.LyricsSidecarsCreated}");

    if (!report.Applied)
    {
        Console.WriteLine();
        Console.WriteLine("Run again with --apply to persist these changes.");
    }
}
