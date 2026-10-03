using Microsoft.Data.Sqlite;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Player.Mix;

namespace Rok.MetadataTool;

/// <summary>Everything the command needs from the outside world, so the whole command can run in tests.</summary>
/// <param name="Out">Standard output.</param>
/// <param name="Error">Standard error.</param>
/// <param name="RunningProcessNames">Names of the running processes.</param>
/// <param name="ReaderFactory">Creates the decoder of one worker.</param>
/// <param name="FileExists">Tells whether an audio file exists.</param>
/// <param name="Time">Clock.</param>
/// <param name="ProcessorCount">Number of logical processors.</param>
internal sealed record MixScanEnvironment(
    TextWriter Out,
    TextWriter Error,
    Func<IReadOnlyCollection<string>> RunningProcessNames,
    Func<WarningCollector, IAudioEnvelopeReader> ReaderFactory,
    Func<string, bool> FileExists,
    TimeProvider Time,
    int ProcessorCount)
{
    /// <summary>Creates the environment of a real run.</summary>
    /// <returns>The console, the real process list, the NAudio decoder and the system clock.</returns>
    public static MixScanEnvironment CreateDefault() =>
        new(
            Console.Out,
            Console.Error,
            RokInstanceProbe.RunningProcessNames,
            CreateReader,
            File.Exists,
            TimeProvider.System,
            Environment.ProcessorCount);

    // The scanner owns the reader: CountingEnvelopeReader disposes it with its worker.
#pragma warning disable IDISP005
    private static IAudioEnvelopeReader CreateReader(WarningCollector collector) =>
        new NAudioEnvelopeReader(new CollectingLogger<NAudioEnvelopeReader>(collector));
#pragma warning restore IDISP005
}

/// <summary>
/// Pre-fills the <c>trackAnalysis</c> table by running the app's own analysis over the whole library.
/// Without <c>--write</c> nothing is written and the report shows what would be written.
/// </summary>
internal static class MixScanCommand
{
    private const int ExitSuccess = 0;
    private const int ExitError = 1;
    private const int ExitRokOpen = 2;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Runs the command with the console and Ctrl+C wired.</summary>
    /// <param name="args">Arguments that follow <c>mix-scan</c>.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        using CancellationTokenSource cts = new();
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += handler;

        try
        {
            return await RunAsync(args, MixScanEnvironment.CreateDefault(), cts.Token);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    /// <summary>Runs the command against an explicit environment.</summary>
    /// <param name="args">Arguments that follow <c>mix-scan</c>.</param>
    /// <param name="env">The outside world.</param>
    /// <param name="ct">Cancels the scan; the rows already written are kept.</param>
    /// <returns>0 on success, 1 on error or interruption, 2 when Rok is open.</returns>
    public static async Task<int> RunAsync(string[] args, MixScanEnvironment env, CancellationToken ct)
    {
        if (args.Length < 1 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage(env.Out);

            return args.Length < 1 ? ExitError : ExitSuccess;
        }

        if (!MixScanOptions.TryParse(args, env.ProcessorCount, out var options, out var error) || options is null)
        {
            await env.Error.WriteLineAsync(error);

            return ExitError;
        }

        if (!File.Exists(options.DatabasePath))
        {
            await env.Error.WriteLineAsync($"Database not found: {options.DatabasePath}");

            return ExitError;
        }

        var state = RokInstanceProbe.Detect(options.DatabasePath, env.RunningProcessNames);

        if (options.Write)
        {
            if (state != RokInstanceState.None)
            {
                await env.Error.WriteLineAsync(state == RokInstanceState.RokProcess
                    ? "Rok is running. Close it before using --write, then retry."
                    : "The database is in use by another program (Rok or a database tool). Close it before using --write, then retry.");

                return ExitRokOpen;
            }
        }

        try
        {
            // Stats mode: SQLite recreates -wal/-shm even for a read-only connection, so when the probe found nobody
            // on the database and no WAL is pending, open it immutable. With the app open, keep the plain read-only mode.
            var immutable = !options.Write && state == RokInstanceState.None && ToolDatabase.CanOpenImmutable(options.DatabasePath);
            var readConnectionString = immutable
                ? ToolDatabase.ImmutableReadOnlyConnectionString(options.DatabasePath)
                : ToolDatabase.ReadOnlyConnectionString(options.DatabasePath);

            return await ScanAsync(options, readConnectionString, env, ct);
        }
        catch (SqliteException ex)
        {
            await env.Error.WriteLineAsync($"SQLite error: {ex.Message}");

            return ExitError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await env.Error.WriteLineAsync($"File error: {ex.Message}");

            return ExitError;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await env.Error.WriteLineAsync($"Unexpected error: {ex.Message}");

            return ExitError;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static async Task<int> ScanAsync(MixScanOptions options, string readConnectionString, MixScanEnvironment env, CancellationToken ct)
    {
        if (!ToolDatabase.TableExists(readConnectionString, "Tracks"))
        {
            await env.Error.WriteLineAsync("This file does not look like a Rok database (no 'Tracks' table).");

            return ExitError;
        }

        if (!MixScanDatabase.HasAnalysisSchema(readConnectionString))
        {
            await env.Error.WriteLineAsync("Database schema is out of date for this tool. Open the database once in Rok (which applies migrations), then retry.");

            return ExitError;
        }

        var now = env.Time.GetLocalNow().DateTime;

        await env.Out.WriteLineAsync($"Database : {options.DatabasePath}");
        await env.Out.WriteLineAsync($"Mode     : {(options.Write ? "WRITE (trackAnalysis will be modified)" : "dry-run (no changes written)")}");

        if (options.Write)
        {
            var backupPath = ToolDatabase.CreateBackup(options.DatabasePath, now);
            await env.Out.WriteLineAsync($"Backup created: {backupPath}");
        }

        var all = await MixScanDatabase.LoadTracksAsync(readConnectionString);
        var tracks = MixScanTracks.Select(all, options.Limit);
        var liveExcluded = all.Count(t => !MixScanTracks.IsAnalysable(t));

        await env.Out.WriteLineAsync($"Tracks   : {tracks.Count} to scan with {Math.Min(options.Parallel, Math.Max(1, tracks.Count))} worker(s)");
        await env.Out.WriteLineAsync();

        var repository = MixScanDatabase.CreateRepository(options.DatabasePath, options.Write, readConnectionString);
        var scanner = new MixScanner(repository, env.ReaderFactory, env.FileExists, env.Time);
        var progress = new ThrottledProgress(env, ProgressInterval);

        var results = await scanner.RunAsync(tracks, options.Parallel, progress, ct);

        progress.Finish();

        var summary = MixScanReport.Aggregate(results, liveExcluded);

        foreach (var line in MixScanReport.FormatReport(summary, options.Write))
            await env.Out.WriteLineAsync(line);

        var failures = MixScanReport.FormatFailures(results);

        if (failures.Count > 0)
        {
            var failuresPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath))!, $"mix-scan-failures-{now:yyyyMMdd_HHmmss}.txt");
            await File.WriteAllLinesAsync(failuresPath, failures, CancellationToken.None);
            await env.Out.WriteLineAsync();
            await env.Out.WriteLineAsync($"Failures listed in: {failuresPath}");
        }

        if (ct.IsCancellationRequested)
        {
            await env.Out.WriteLineAsync();
            await env.Out.WriteLineAsync("Interrupted: the rows already written are kept, run again to complete the scan.");

            return ExitError;
        }

        return ExitSuccess;
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("Usage: Rok.MetadataTool mix-scan <database.sqlite> [--write] [--limit N] [--parallel N]");
        output.WriteLine();
        output.WriteLine("  Runs the Mix analysis of the app over the library (intro and outro of every");
        output.WriteLine("  non-live track) to pre-fill the trackAnalysis table, so transitions are");
        output.WriteLine("  aligned from the first listen. Rows already valid are skipped, so a run can");
        output.WriteLine("  be interrupted (Ctrl+C) and resumed.");
        output.WriteLine();
        output.WriteLine("Arguments:");
        output.WriteLine("  <database.sqlite>  Path to an up-to-date Rok SQLite database.");
        output.WriteLine("  --write            Persist the rows. Refused while Rok is open; a timestamped");
        output.WriteLine("                     .bak backup is taken first. Without it, nothing is written");
        output.WriteLine("                     and the report shows what would be written.");
        output.WriteLine("  --limit N          Scan only the first N non-live tracks (by id).");
        output.WriteLine("  --parallel N       Number of decoding workers (default: processors - 1).");
        output.WriteLine();
        output.WriteLine("  Opening Rok during a --write scan is not detected: close it before starting.");
        output.WriteLine("  Exit codes: 0 success, 1 error or interruption, 2 Rok is open.");
    }

    private sealed class ThrottledProgress(MixScanEnvironment env, TimeSpan interval) : IProgress<MixScanProgress>
    {
        private readonly Lock _lock = new();
        private long _lastTimestamp = env.Time.GetTimestamp();
        private int _lastLength;
        private bool _printed;

        public void Report(MixScanProgress value)
        {
            lock (_lock)
            {
                if (value.Processed < value.Total && env.Time.GetElapsedTime(_lastTimestamp) < interval)
                    return;

                _lastTimestamp = env.Time.GetTimestamp();

                var line = MixScanReport.FormatProgress(value);
                env.Out.Write($"\r{line.PadRight(_lastLength)}");
                _lastLength = line.Length;
                _printed = true;
            }
        }

        public void Finish()
        {
            lock (_lock)
            {
                if (_printed)
                    env.Out.WriteLine();
            }
        }
    }
}