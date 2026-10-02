using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Interfaces;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;
using Rok.Domain.Enums;
using Rok.Infrastructure.Player.Mix;

namespace Rok.MetadataTool;

/// <summary>
/// Measures the tempo detector against the BPM tags of a library. The database is opened read-only and
/// nothing is ever written: it is a measurement tool used to tune the detection thresholds.
/// </summary>
internal static class BpmCheckCommand
{
    private const double Tolerance = 0.02;
    private const int WorstCount = 20;
    private const double MinTagBpm = 40;
    private const double MaxTagBpm = 250;

    /// <summary>Runs the command.</summary>
    /// <param name="args">Arguments that follow <c>bpm-check</c>: the database path and optional <c>--limit N</c> and <c>--sweep</c>.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length < 1 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();

            return args.Length < 1 ? 1 : 0;
        }

        string databasePath = args[0];

        if (!TryParseLimit(args, out int? limit))
        {
            Console.Error.WriteLine("--limit expects a positive integer.");

            return 1;
        }

        if (!File.Exists(databasePath))
        {
            Console.Error.WriteLine($"Database not found: {databasePath}");

            return 1;
        }

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            List<TaggedTrack> loaded = LoadTracks(databasePath, limit);
            List<TaggedTrack> tracks = loaded.Where(t => t.Bpm is >= MinTagBpm and <= MaxTagBpm).ToList();
            bool sweep = args.Contains("--sweep");

            Console.WriteLine($"Database : {databasePath} (read-only)");
            Console.WriteLine($"Tracks   : {loaded.Count} with a BPM tag{(limit is null ? string.Empty : $" (limit {limit})")}");
            Console.WriteLine($"Tags out of range (excluded) : {loaded.Count - tracks.Count} (outside [{MinTagBpm:F0}, {MaxTagBpm:F0}])");
            Console.WriteLine();

            List<SweepWindow> windows = [];
            BpmCheckReport report = await MeasureAsync(tracks, sweep ? windows : null, cts.Token);
            PrintReport(report);

            PrintMixPoints(MixPointStats.Aggregate(report.MixPoints, MixThresholds.MinMixPointScore));

            if (sweep)
                PrintSweep(BpmSweep.Aggregate(windows, BpmSweep.DefaultThresholds, Tolerance));
        }
        catch (SqliteException ex)
        {
            Console.Error.WriteLine($"SQLite error: {ex.Message}");

            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");

            return 1;
        }

        return 0;
    }

    private static bool TryParseLimit(string[] args, out int? limit)
    {
        limit = null;
        int index = Array.IndexOf(args, "--limit");

        if (index < 0)
            return true;

        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int value) || value <= 0)
            return false;

        limit = value;

        return true;
    }

    private static List<TaggedTrack> LoadTracks(string databasePath, int? limit)
    {
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, musicFile, bpm FROM Tracks WHERE bpm > 0 ORDER BY id" + (limit is null ? ";" : " LIMIT $limit;");

        if (limit is not null)
            command.Parameters.AddWithValue("$limit", limit.Value);

        List<TaggedTrack> tracks = [];

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
            tracks.Add(new TaggedTrack(reader.GetInt64(0), reader.GetString(1), reader.GetDouble(2)));

        return tracks;
    }

    private static async Task<BpmCheckReport> MeasureAsync(List<TaggedTrack> tracks, List<SweepWindow>? sweepWindows, CancellationToken ct)
    {
        using NAudioEnvelopeReader reader = new(NullLogger<NAudioEnvelopeReader>.Instance);
        BpmCheckReport report = new();
        TimeSpan span = TimeSpan.FromSeconds(MixThresholds.AnalysisWindowSeconds);
        int done = 0;

        foreach (TaggedTrack track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            done++;

            if (!File.Exists(track.MusicFile))
            {
                report.MissingFiles++;

                continue;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (EAudioEdge edge in new[] { EAudioEdge.Head, EAudioEdge.Tail })
            {
                AudioEdgeSignal? signal = await reader.ReadAsync(track.MusicFile, edge, span, includeMonoSamples: true, ct);

                if (signal?.MonoSamples is null)
                {
                    report.Unreadable++;

                    continue;
                }

                OnsetCurve? curve = BeatGridDetector.IsLongEnough(signal.MonoSamples.Length, signal.MonoSampleRate)
                    ? OnsetCurve.Compute(signal.MonoSamples, signal.MonoSampleRate)
                    : null;
                TempoDetection detection = curve is null ? TempoDetection.None : BeatGridDetector.Detect(curve, signal.StartSeconds, knownBpm: null);
                report.Add(edge, track, detection);
                sweepWindows?.Add(MeasureRaw(curve, edge == EAudioEdge.Head, track.Bpm));

                if (edge == EAudioEdge.Tail)
                    report.MixPoints.Add(MeasureMixPoint(signal, curve, track));
            }

            stopwatch.Stop();
            report.TrackTime += stopwatch.Elapsed;
            report.TracksMeasured++;

            if (done % 25 == 0 || done == tracks.Count)
                Console.Write($"\rMeasured {done}/{tracks.Count}   ");
        }

        Console.WriteLine();
        Console.WriteLine();

        return report;
    }

    private static MixPointSample MeasureMixPoint(AudioEdgeSignal signal, OnsetCurve? curve, TaggedTrack track)
    {
        if (curve is null)
            return new MixPointSample(null);

        // Same as the app: the tag takes precedence, so only the beat phase is detected.
        TempoDetection tagged = BeatGridDetector.Detect(curve, signal.StartSeconds, track.Bpm);

        if (tagged.FirstBeatSeconds is not { } firstBeat)
            return new MixPointSample(null);

        double? downbeat = DownbeatEstimator.Estimate(curve, signal.StartSeconds, track.Bpm, firstBeat);
        BeatGrid grid = new(track.Bpm, firstBeat, 1, BpmSource.Tag, downbeat);
        OutroCues? outro = MixCueDetector.DetectOutro(signal.Envelope);

        if (outro is null)
            return new MixPointSample(null);

        MixPoint? point = MixPointDetector.Detect(signal.Envelope, curve, signal.StartSeconds, grid, outro.MusicEndSeconds);

        return new MixPointSample(point?.Score);
    }

    private static SweepWindow MeasureRaw(OnsetCurve? curve, bool intro, double tag)
    {
        if (curve is null)
            return new SweepWindow(intro, 0, null, tag);

        TempoEstimate? estimate = TempoEstimator.Estimate(curve);

        if (estimate is null)
            return new SweepWindow(intro, 0, null, tag);

        return new SweepWindow(intro, estimate.Confidence, BeatPhaseEstimator.RefineTempo(curve, estimate.Bpm), tag);
    }

    private static void PrintSweep(IReadOnlyList<SweepRow> rows)
    {
        Console.WriteLine();
        Console.WriteLine("Confidence threshold sweep (not rejected % / correct among not rejected %):");
        Console.WriteLine($"{"threshold",-10}{"intro",-18}{"outro",-18}{"all",-18}");

        foreach (SweepRow row in rows)
            Console.WriteLine($"{row.Threshold,-10:F3}{Format(row.Intro),-18}{Format(row.Outro),-18}{Format(row.All),-18}");
    }

    private static void PrintMixPoints(MixPointSummary summary)
    {
        double Rate(int count) => summary.Total == 0 ? 0 : (double)count / summary.Total;

        Console.WriteLine();
        Console.WriteLine($"Mix point (outro, tag tempo like the app, retained at score >= {MixThresholds.MinMixPointScore:F2}):");
        Console.WriteLine($"  Outros analysed     : {summary.Total}");
        Console.WriteLine($"  With a candidate    : {summary.WithCandidate} ({Rate(summary.WithCandidate):P1})");
        Console.WriteLine($"  Retained            : {summary.Retained} ({Rate(summary.Retained):P1})");
        Console.WriteLine("  Score distribution (candidates, buckets of 0.1):");

        foreach ((double from, int count) in summary.Histogram)
            Console.WriteLine($"    [{from:F1}, {from + 0.1:F1}{(from >= 1.4 ? "+" : ")")} {count,5}  {new string('#', Math.Min(count, 60))}");

        Console.WriteLine("  Threshold sweep (share of outros retained):");

        foreach ((double threshold, double retainedRate) in summary.Sweep)
            Console.WriteLine($"    {threshold,5:F2}  {retainedRate:P1}");
    }

    private static string Format(SweepStats stats) => $"{stats.NotRejectedRate:P1} / {stats.CorrectRate:P1}";

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Rok.MetadataTool bpm-check <database.sqlite> [--limit N] [--sweep]");
        Console.WriteLine();
        Console.WriteLine("  Runs the tempo detector on the intro and outro of every track that has a BPM");
        Console.WriteLine("  tag and compares the result with the tag (+/- 2 %, octave errors accepted).");
        Console.WriteLine("  The database is opened read-only; nothing is written.");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <database.sqlite>  Path to a Rok SQLite database.");
        Console.WriteLine("  --limit N          Only measure the first N tracks.");
        Console.WriteLine("  --sweep            Also print, for confidence thresholds 0.05 to 0.30 (step 0.025), the share");
        Console.WriteLine("                     of windows not rejected and the accuracy among them (one decode per track).");
        Console.WriteLine();
        Console.WriteLine("  Also reports, on the outro, the share of tracks with a retained mix point and the distribution");
        Console.WriteLine("  of the mix point scores (histogram and threshold sweep), using the tag tempo like the app.");
        Console.WriteLine();
        Console.WriteLine("Tracks whose tag is outside [40, 250] BPM are excluded from the measure.");
    }

    private static void PrintReport(BpmCheckReport report)
    {
        Console.WriteLine($"Tracks measured         : {report.TracksMeasured}");
        Console.WriteLine($"Files missing on disk   : {report.MissingFiles}");
        Console.WriteLine($"Windows unreadable      : {report.Unreadable}");
        Console.WriteLine($"Mean time per track     : {(report.TracksMeasured == 0 ? 0 : report.TrackTime.TotalMilliseconds / report.TracksMeasured):F0} ms");
        Console.WriteLine();

        PrintStats("Intro", report.Intro);
        PrintStats("Outro", report.Outro);
        PrintStats("All windows", new WindowStats(report.Intro.Total + report.Outro.Total, report.Intro.Rejected + report.Outro.Rejected, report.Intro.Correct + report.Outro.Correct));

        List<Deviation> worst = report.Deviations.Where(d => !d.Correct).OrderByDescending(d => d.Relative).Take(WorstCount).ToList();

        if (worst.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine($"Worst {worst.Count} deviations (tag -> detected):");

        foreach (Deviation d in worst)
            Console.WriteLine($"  #{d.TrackId,-6} {d.Edge,-4} {d.Tag,7:F1} -> {d.Detected,7:F1}  ({d.Relative:P0})  {d.File}");
    }

    private static void PrintStats(string label, WindowStats stats)
    {
        int accepted = stats.Total - stats.Rejected;
        double acceptedRate = stats.Total == 0 ? 0 : (double)accepted / stats.Total;
        double correctRate = accepted == 0 ? 0 : (double)stats.Correct / accepted;

        Console.WriteLine($"{label,-12}: {stats.Total} windows, not rejected {acceptedRate:P1}, correct among not rejected {correctRate:P1}, rejected {1 - acceptedRate:P1}");
    }

    private sealed record TaggedTrack(long Id, string MusicFile, double Bpm);

    private sealed record Deviation(long TrackId, string Edge, string File, double Tag, double Detected, double Relative, bool Correct);

    private sealed record WindowStats(int Total, int Rejected, int Correct);

    private sealed class BpmCheckReport
    {
        private int _introTotal;
        private int _introRejected;
        private int _introCorrect;
        private int _outroTotal;
        private int _outroRejected;
        private int _outroCorrect;

        public int TracksMeasured { get; set; }

        public int MissingFiles { get; set; }

        public int Unreadable { get; set; }

        public TimeSpan TrackTime { get; set; }

        public List<Deviation> Deviations { get; } = [];

        public List<MixPointSample> MixPoints { get; } = [];

        public WindowStats Intro => new(_introTotal, _introRejected, _introCorrect);

        public WindowStats Outro => new(_outroTotal, _outroRejected, _outroCorrect);

        public void Add(EAudioEdge edge, TaggedTrack track, TempoDetection detection)
        {
            bool intro = edge == EAudioEdge.Head;
            bool rejected = detection.Bpm is null;
            bool correct = detection.Bpm is { } bpm && TempoMatch.IsOctaveEquivalent(bpm, track.Bpm, Tolerance);

            if (intro)
            {
                _introTotal++;
                _introRejected += rejected ? 1 : 0;
                _introCorrect += correct ? 1 : 0;
            }
            else
            {
                _outroTotal++;
                _outroRejected += rejected ? 1 : 0;
                _outroCorrect += correct ? 1 : 0;
            }

            if (detection.Bpm is { } detected)
                Deviations.Add(new Deviation(track.Id, intro ? "head" : "tail", track.MusicFile, track.Bpm, detected, RelativeDeviation(detected, track.Bpm), correct));
        }

        private static double RelativeDeviation(double detected, double tag) =>
            new[] { 1.0, 2.0, 0.5 }.Min(factor => Math.Abs(detected - (tag * factor)) / (tag * factor));
    }
}