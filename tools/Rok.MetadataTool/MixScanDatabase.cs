using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Dto;
using Rok.Application.Interfaces.Repositories;
using Rok.Infrastructure.Repositories;

namespace Rok.MetadataTool;

/// <summary>Database access of the Mix scan: schema check, track loading and the analysis repository.</summary>
internal static class MixScanDatabase
{
    private static readonly Lock HandlerLock = new();
    private static bool _handlerRegistered;

    /// <summary>True when the database carries the last Mix analysis column (Migration19).</summary>
    public static bool HasAnalysisSchema(string readConnectionString)
    {
        return ToolDatabase.ColumnExists(readConnectionString, "trackAnalysis", "outroMixPointScore");
    }

    /// <summary>Loads every track the way the app does, over read-only connections.</summary>
    public static async Task<IReadOnlyList<TrackDto>> LoadTracksAsync(string readConnectionString)
    {
        RegisterTypeHandler();

        using SqliteConnection foreground = new(readConnectionString);
        using SqliteConnection background = new(readConnectionString);
        foreground.Open();
        background.Open();

        TrackRepository repository = new(foreground, background, NullLogger<TrackRepository>.Instance, TimeProvider.System);
        var tracks = await repository.GetAllAsync();

        return tracks.Select(MixScanTracks.ToDto).ToList();
    }

    /// <summary>The real repository when writing, a dry-run one over a read-only repository otherwise.</summary>
    public static ITrackAnalysisRepository CreateRepository(string databasePath, bool write, string readConnectionString)
    {
        if (write)
        {
            string writeConnectionString = ToolDatabase.AnalysisWriteConnectionString(databasePath);

            return new TrackAnalysisRepository(() => new SqliteConnection(writeConnectionString));
        }

        return new DryRunTrackAnalysisRepository(new TrackAnalysisRepository(() => new SqliteConnection(readConnectionString)));
    }

    private static void RegisterTypeHandler()
    {
        lock (HandlerLock)
        {
            if (_handlerRegistered)
            {
                return;
            }

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            _handlerRegistered = true;
        }
    }
}