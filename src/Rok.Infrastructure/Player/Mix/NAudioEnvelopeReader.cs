using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Rok.Application.Interfaces;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Mix.Tempo;

namespace Rok.Infrastructure.Player.Mix;

/// <summary>Measures the loudness envelope of a track edge, and optionally builds its mono signal, with a single NAudio decode on a low-priority thread.</summary>
public sealed class NAudioEnvelopeReader : IAudioEnvelopeReader, IDisposable
{
    private const int BlockSize = 4096;
    private const int FileBufferSize = 64 * 1024;

    private readonly ILogger<NAudioEnvelopeReader> _logger;
    private readonly Lock _workerLock = new();
    private LowPriorityWorker? _worker;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="NAudioEnvelopeReader"/> class.</summary>
    /// <param name="logger">Logger.</param>
    public NAudioEnvelopeReader(ILogger<NAudioEnvelopeReader> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AudioEdgeSignal?> ReadAsync(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct)
    {
        try
        {
            return await GetWorker().RunAsync(token => Read(path, edge, span, includeMonoSamples, token), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: could not read the {Edge} envelope of {Path}", edge, path);

            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LowPriorityWorker? worker;

        lock (_workerLock)
        {
            _disposed = true;
            worker = _worker;
            _worker = null;
        }

        worker?.Dispose();
    }

    private LowPriorityWorker GetWorker()
    {
        lock (_workerLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _worker ??= new LowPriorityWorker();
        }
    }

    private AudioEdgeSignal? Read(string path, EAudioEdge edge, TimeSpan span, bool includeMonoSamples, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileBufferSize, FileOptions.SequentialScan);
            using var reader = new AudioFileReader(stream);

            ISampleProvider samples = reader;
            var total = reader.TotalTime;

            if (edge == EAudioEdge.Tail)
                reader.CurrentTime = total > span ? total - span : TimeSpan.Zero;

            var startSeconds = reader.CurrentTime.TotalSeconds;
            var channels = reader.WaveFormat.Channels;
            var accumulator = new RmsEnvelopeAccumulator(reader.WaveFormat.SampleRate);
            var decimator = includeMonoSamples ? new MonoDecimator(reader.WaveFormat.SampleRate) : null;
            var framesToRead = (long)(span.TotalSeconds * reader.WaveFormat.SampleRate);
            var blockFrames = BlockSize / channels;
            var buffer = new float[blockFrames * channels];
            long framesRead = 0;

            while (framesRead < framesToRead)
            {
                ct.ThrowIfCancellationRequested();

                var wanted = (int)Math.Min(blockFrames, framesToRead - framesRead) * channels;
                var read = samples.Read(buffer.AsSpan(0, wanted));

                if (read <= 0)
                    break;

                accumulator.Add(buffer.AsSpan(0, read), channels);
                decimator?.Add(buffer.AsSpan(0, read), channels);
                framesRead += read / channels;
            }

            var envelope = accumulator.Build(startSeconds, total.TotalSeconds);

            return new AudioEdgeSignal(envelope, decimator?.Build(), decimator?.OutputSampleRate ?? 0, startSeconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: could not decode the {Edge} of {Path}", edge, path);

            return null;
        }
        finally
        {
            _logger.LogDebug("Mix: decoded the {Edge} of {Path} in {ElapsedMs} ms", edge, path, stopwatch.ElapsedMilliseconds);
        }
    }
}