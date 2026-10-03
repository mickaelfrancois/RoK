using Dapper;
using Microsoft.Data.Sqlite;
using Rok.MetadataTool;

namespace Rok.Infrastructure.UnitTests.Tools;

public class RokInstanceProbeTests
{
    [Fact(DisplayName = "probe_detects_a_running_rok_process")]
    public void Detect_RokProcessListed_ReturnsRokProcess()
    {
        using TempRokDatabase database = new();

        RokInstanceState state = RokInstanceProbe.Detect(database.DatabasePath, () => ["explorer", "rok"]);

        Assert.Equal(RokInstanceState.RokProcess, state);
    }

    [Fact(DisplayName = "probe_detects_a_database_held_by_another_connection")]
    public void Detect_DatabaseOpenElsewhere_ReturnsDatabaseInUse()
    {
        using TempRokDatabase database = new();
        using SqliteConnection holder = database.Open();
        holder.ExecuteScalar<long>("SELECT count(*) FROM tracks;");

        RokInstanceState state = RokInstanceProbe.Detect(database.DatabasePath, () => ["explorer"]);

        Assert.Equal(RokInstanceState.DatabaseInUse, state);
    }

    [Fact(DisplayName = "probe_reports_none_when_closed")]
    public void Detect_NothingRunning_ReturnsNone()
    {
        using TempRokDatabase database = new();
        SqliteConnection.ClearAllPools();

        RokInstanceState state = RokInstanceProbe.Detect(database.DatabasePath, () => ["explorer"]);

        Assert.Equal(RokInstanceState.None, state);
    }

    [Fact(DisplayName = "probe_reports_none_when_the_shared_memory_file_is_absent")]
    public void Detect_NoSharedMemoryFile_ReturnsNone()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rok-probe-{Guid.NewGuid():N}.sqlite");

        RokInstanceState state = RokInstanceProbe.Detect(path, () => []);

        Assert.Equal(RokInstanceState.None, state);
    }

    [Fact(DisplayName = "probe_does_not_flag_a_process_that_only_contains_rok")]
    public void Detect_SimilarProcessName_IsNotRok()
    {
        using TempRokDatabase database = new();
        SqliteConnection.ClearAllPools();

        RokInstanceState state = RokInstanceProbe.Detect(database.DatabasePath, () => ["Rok.MetadataTool"]);

        Assert.Equal(RokInstanceState.None, state);
    }
}