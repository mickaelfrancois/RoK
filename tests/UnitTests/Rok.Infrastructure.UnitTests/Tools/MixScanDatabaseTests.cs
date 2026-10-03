using Dapper;
using Microsoft.Data.Sqlite;
using Rok.Application.Dto;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player.Mix;
using Rok.Domain.Entities;
using Rok.MetadataTool;

namespace Rok.Infrastructure.UnitTests.Tools;

public class MixScanDatabaseTests
{
    [Fact(DisplayName = "load_tracks_maps_file_date_size_and_live_flags")]
    public async Task LoadTracksAsync_SeededDatabase_MapsAnalysisFields()
    {
        using TempRokDatabase database = new();

        IReadOnlyList<TrackDto> tracks = await MixScanDatabase.LoadTracksAsync(ToolDatabase.ReadOnlyConnectionString(database.DatabasePath));

        Assert.Equal(4, tracks.Count);
        TrackDto studio = tracks.Single(t => t.Id == 2);
        Assert.Equal(1200, studio.Size);
        Assert.Equal(@"C:\music\t2.mp3", studio.MusicFile);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), studio.FileDate);
        Assert.True(tracks.Single(t => t.Id == 3).IsLive);
        Assert.True(tracks.Single(t => t.Id == 4).IsAlbumLive);
        Assert.False(studio.IsLive);
        Assert.False(studio.IsAlbumLive);
    }

    [Fact(DisplayName = "has_analysis_schema_is_true_on_a_migrated_database")]
    public void HasAnalysisSchema_MigratedDatabase_ReturnsTrue()
    {
        using TempRokDatabase database = new();

        Assert.True(MixScanDatabase.HasAnalysisSchema(ToolDatabase.ReadOnlyConnectionString(database.DatabasePath)));
    }

    [Fact(DisplayName = "has_analysis_schema_is_false_without_the_last_column")]
    public void HasAnalysisSchema_OldDatabase_ReturnsFalse()
    {
        using TempRokDatabase database = new();

        using (SqliteConnection connection = database.Open())
        {
            connection.Execute("ALTER TABLE trackAnalysis DROP COLUMN outroMixPointScore;");
        }

        Assert.False(MixScanDatabase.HasAnalysisSchema(ToolDatabase.ReadOnlyConnectionString(database.DatabasePath)));
    }

    [Fact(DisplayName = "stats_repository_serves_rows_without_writing_to_the_database")]
    public async Task CreateRepository_StatsMode_NeverWrites()
    {
        using TempRokDatabase database = new();
        database.Checkpoint();
        string before = database.Sha256();
        ITrackAnalysisRepository repository = MixScanDatabase.CreateRepository(database.DatabasePath, write: false, ToolDatabase.ReadOnlyConnectionString(database.DatabasePath));

        await repository.UpsertAsync(new TrackAnalysisEntity { TrackId = 1, AlgorithmVersion = TrackAnalysisVersion.Current }, CancellationToken.None);
        TrackAnalysisEntity? served = await repository.GetAsync(1, CancellationToken.None);
        SqliteConnection.ClearAllPools();

        Assert.NotNull(served);
        Assert.Equal(0, database.CountAnalysisRows());
        Assert.Equal(before, database.Sha256());
    }

    [Fact(DisplayName = "write_repository_persists_rows_in_the_database")]
    public async Task CreateRepository_WriteMode_Persists()
    {
        using TempRokDatabase database = new();
        ITrackAnalysisRepository repository = MixScanDatabase.CreateRepository(database.DatabasePath, write: true, ToolDatabase.ReadOnlyConnectionString(database.DatabasePath));

        await repository.UpsertAsync(new TrackAnalysisEntity { TrackId = 1, AlgorithmVersion = TrackAnalysisVersion.Current }, CancellationToken.None);
        SqliteConnection.ClearAllPools();

        Assert.Equal(1, database.CountAnalysisRows());
    }
}