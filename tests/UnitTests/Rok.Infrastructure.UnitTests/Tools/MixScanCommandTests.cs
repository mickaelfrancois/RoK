using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Rok.Application.Interfaces;
using Rok.Application.Player.Mix;
using Rok.MetadataTool;

namespace Rok.Infrastructure.UnitTests.Tools;

public sealed class MixScanCommandTests : IDisposable
{
    private const int SampleRate = 8000;

    private readonly TempRokDatabase _database = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _database.Dispose();
        _out.Dispose();
        _error.Dispose();
    }

    private static float[] Sine(double seconds)
    {
        var samples = new float[(int)(seconds * SampleRate)];

        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)Math.Sin(2 * Math.PI * 440 * i / SampleRate);

        return samples;
    }

    private static RmsEnvelope Envelope(float[] mono, double start, double length)
    {
        var accumulator = new RmsEnvelopeAccumulator(SampleRate);
        accumulator.Add(mono, 1);

        return accumulator.Build(start, length);
    }

    private static RmsEnvelope HeadEnvelope() => Envelope([.. new float[3 * SampleRate], .. Sine(27)], 0, 200);

    private static RmsEnvelope TailEnvelope() => Envelope([.. Sine(25), .. new float[5 * SampleRate]], 170, 200);

    private MixScanEnvironment Environment(
        IReadOnlyCollection<string>? processes = null,
        Func<string, bool>? fileExists = null,
        Func<WarningCollector, IAudioEnvelopeReader>? readerFactory = null,
        TimeProvider? time = null) =>
        new(
            _out,
            _error,
            () => processes ?? [],
            readerFactory ?? (_ => new FakeEnvelopeReader()),
            fileExists ?? (_ => true),
            time ?? TimeProvider.System,
            4);

    private string[] BackupFiles() => Directory.GetFiles(_database.DirectoryPath, "*.bak");

    private void DeleteSideFiles()
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var path = $"{_database.DatabasePath}{suffix}";

            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private long CountAnalysisRows(string path)
    {
        using SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();

        return connection.ExecuteScalar<long>("SELECT count(*) FROM trackAnalysis;");
    }

    [Fact(DisplayName = "stats_mode_leaves_the_database_untouched")]
    public async Task RunAsync_StatsMode_LeavesTheDatabaseUntouched()
    {
        // Arrange
        _database.Checkpoint();
        var hashBefore = _database.Sha256();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);
        Assert.Equal(hashBefore, _database.Sha256());
        Assert.Equal(0, _database.CountAnalysisRows());
        Assert.Empty(BackupFiles());
        Assert.Contains("Mix scan report (dry run, nothing written)", _out.ToString());
        Assert.Equal(string.Empty, _error.ToString());
    }

    [Fact(DisplayName = "stats_mode_creates_no_wal_or_shm_side_files_next_to_a_cleanly_closed_database")]
    public async Task RunAsync_StatsMode_CreatesNoSideFiles()
    {
        // Arrange
        _database.Checkpoint();
        SqliteConnection.ClearAllPools();
        DeleteSideFiles();
        var hashBefore = _database.Sha256();
        var filesBefore = Directory.GetFiles(_database.DirectoryPath).Order().ToArray();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);
        Assert.False(File.Exists($"{_database.DatabasePath}-wal"));
        Assert.False(File.Exists($"{_database.DatabasePath}-shm"));
        Assert.Equal(filesBefore, Directory.GetFiles(_database.DirectoryPath).Order().ToArray());
        Assert.Equal(hashBefore, _database.Sha256());
    }

    [Fact(DisplayName = "stats_mode_still_runs_with_the_plain_read_only_connection_while_rok_is_open")]
    public async Task RunAsync_StatsModeWithRokRunning_StillScans()
    {
        // Arrange
        using var holder = _database.Open();
        holder.ExecuteScalar<long>("SELECT count(*) FROM Tracks;");

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(["Rok"]), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Mix scan report (dry run, nothing written)", _out.ToString());
        Assert.Equal(0, _database.CountAnalysisRows());
    }

    [Fact(DisplayName = "write_mode_with_a_blocked_backup_path_exits_with_code_1_and_leaves_the_database_intact")]
    public async Task RunAsync_WriteModeWithBlockedBackup_ExitsWithCode1()
    {
        // Arrange
        _database.Checkpoint();
        var hashBefore = _database.Sha256();
        FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var now = time.GetLocalNow().DateTime;
        Directory.CreateDirectory($"{_database.DatabasePath}.{now:yyyyMMdd_HHmmss}.bak");

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, "--write"], Environment(time: time), CancellationToken.None);

        // Assert
        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, _error.ToString());
        Assert.DoesNotContain("   at ", _error.ToString());
        Assert.Equal(hashBefore, _database.Sha256());
        Assert.Equal(0, _database.CountAnalysisRows());
    }

    [Fact(DisplayName = "reader_factory_failure_exits_with_code_1_without_a_stack_trace")]
    public async Task RunAsync_ReaderFactoryThrows_ExitsWithCode1()
    {
        // Arrange
        _database.Checkpoint();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(readerFactory: _ => throw new InvalidOperationException("no decoder")), CancellationToken.None);

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("no decoder", _error.ToString());
        Assert.DoesNotContain("   at ", _error.ToString());
    }

    [Fact(DisplayName = "failures_file_write_error_exits_with_code_1_with_a_clear_message")]
    public async Task RunAsync_FailuresFileBlocked_ExitsWithCode1()
    {
        // Arrange
        _database.Checkpoint();
        FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero));
        var now = time.GetLocalNow().DateTime;
        Directory.CreateDirectory(Path.Combine(_database.DirectoryPath, $"mix-scan-failures-{now:yyyyMMdd_HHmmss}.txt"));

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(fileExists: path => !path.EndsWith("t2.mp3", StringComparison.Ordinal), time: time), CancellationToken.None);

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("File error", _error.ToString());
        Assert.DoesNotContain("   at ", _error.ToString());
    }

    [Fact(DisplayName = "write_mode_refuses_when_rok_is_running")]
    public async Task RunAsync_WriteModeWithRokRunning_RefusesWithoutTouchingTheDatabase()
    {
        // Arrange
        _database.Checkpoint();
        var hashBefore = _database.Sha256();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, "--write"], Environment(["Rok"]), CancellationToken.None);

        // Assert
        Assert.Equal(2, code);
        Assert.Contains("Rok is running", _error.ToString());
        Assert.Empty(BackupFiles());
        Assert.Equal(hashBefore, _database.Sha256());
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact(DisplayName = "write_mode_refuses_when_the_database_is_held_by_another_connection")]
    public async Task RunAsync_WriteModeWithDatabaseInUse_Refuses()
    {
        // Arrange
        using var holder = _database.Open();
        holder.ExecuteScalar<long>("SELECT count(*) FROM Tracks;");

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, "--write"], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(2, code);
        Assert.Contains("in use", _error.ToString());
        Assert.Empty(BackupFiles());
    }

    [Fact(DisplayName = "write_mode_backs_up_before_the_first_write")]
    public async Task RunAsync_WriteMode_BacksUpBeforeTheFirstWrite()
    {
        // Arrange
        _database.Checkpoint();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, "--write"], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);

        var backup = Assert.Single(BackupFiles());
        Assert.Equal(0, CountAnalysisRows(backup));
        Assert.True(_database.CountAnalysisRows() > 0);
        Assert.Contains("Backup created", _out.ToString());
        Assert.Contains("Mix scan report (rows written)", _out.ToString());
    }

    [Fact(DisplayName = "write_mode_lists_missing_files_in_the_failures_file")]
    public async Task RunAsync_WriteModeWithMissingFile_ListsItInTheFailuresFile()
    {
        // Arrange
        _database.Checkpoint();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, "--write"], Environment(fileExists: path => !path.EndsWith("t2.mp3", StringComparison.Ordinal)), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);

        var failuresFile = Assert.Single(Directory.GetFiles(_database.DirectoryPath, "mix-scan-failures-*.txt"));
        var content = await File.ReadAllTextAsync(failuresFile);
        Assert.Contains(@"C:\music\t2.mp3", content);
        Assert.DoesNotContain(@"C:\music\t3.mp3", content);
        Assert.DoesNotContain(@"C:\music\t4.mp3", content);
        Assert.Contains(failuresFile, _out.ToString());
    }

    [Fact(DisplayName = "no_failures_file_is_written_when_every_track_is_analysed")]
    public async Task RunAsync_WithoutFailures_WritesNoFailuresFile()
    {
        // Arrange
        _database.Checkpoint();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);
        Assert.Empty(Directory.GetFiles(_database.DirectoryPath, "mix-scan-failures-*.txt"));
    }

    [Fact(DisplayName = "cancelled_scan_exits_with_code_1_and_reports_the_interruption")]
    public async Task RunAsync_Cancelled_ExitsWithCode1()
    {
        // Arrange
        _database.Checkpoint();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath], Environment(), cts.Token);

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("Interrupted", _out.ToString());
    }

    [Theory(DisplayName = "invalid_invocation_exits_with_code_1")]
    [InlineData("--parallel", "0")]
    [InlineData("--unknown")]
    public async Task RunAsync_InvalidArguments_ExitsWithCode1(params string[] extra)
    {
        // Act
        var code = await MixScanCommand.RunAsync([_database.DatabasePath, .. extra], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, _error.ToString());
    }

    [Fact(DisplayName = "missing_database_exits_with_code_1")]
    public async Task RunAsync_MissingDatabase_ExitsWithCode1()
    {
        // Act
        var code = await MixScanCommand.RunAsync([Path.Combine(_database.DirectoryPath, "absent.sqlite"), "--write"], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("Database not found", _error.ToString());
    }

    [Fact(DisplayName = "help_prints_the_usage_and_exits_with_code_0")]
    public async Task RunAsync_Help_PrintsUsage()
    {
        // Act
        var code = await MixScanCommand.RunAsync(["--help"], Environment(), CancellationToken.None);

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("mix-scan", _out.ToString());
    }

    private sealed class FakeEnvelopeReader : IAudioEnvelopeReader
    {
        public Task<AudioEdgeSignal?> ReadAsync(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct)
        {
            var envelope = edge == EAudioEdge.Head ? HeadEnvelope() : TailEnvelope();

            return Task.FromResult<AudioEdgeSignal?>(new AudioEdgeSignal(envelope, null, 0, envelope.StartSeconds));
        }
    }
}