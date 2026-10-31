using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.Compression;
using NAudio.Wave.SampleProviders;
using Rok.Application.Dto;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.Player.Streaming;

internal sealed class StreamingPlayback : IDisposable
{
    public event EventHandler<string>? MetadataChanged;
    public event EventHandler? PlaybackEnded;
    public event EventHandler<bool>? BufferingChanged;

    /// <summary>Raised on the thread pool once a failed output has been released.</summary>
    public event EventHandler<AudioOutputHandle>? OutputLost;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly Func<ISampleProvider, AudioOutputHandle> _openOutput;
    private readonly Lock _outputLock = new();

    private IcyStreamHandler? _icy;
    private IWaveProvider? _decoded;
    private BufferedWaveProvider? _buffer;
    private AudioOutputHandle? _output;
    private VolumeSampleProvider? _volumeProvider;
    private float _volume = 1f;
    private readonly CancellationTokenSource _stopCts = new();
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private IMp3FrameDecompressor? _mp3Decompressor;
    private readonly Func<WaveFormat, IMp3FrameDecompressor> _createMp3Decompressor;
    private readonly Func<string, IWaveProvider> _openMediaFoundationUrl;
    private readonly Func<Stream, IWaveProvider> _openMediaFoundationStream;
    private readonly TimeSpan _stopTimeout;

    private const double BufferDurationSeconds = 15.0;
    private const double PreBufferSeconds = 3.0;
    private const double ResumeAfterUnderflowSeconds = 2.0;
    private const double BufferingTriggerSeconds = 0.5;
    private const double TerminalNoBytesSeconds = 5.0;

    public bool IsBuffering { get; private set; }

    /// <summary>Output currently open, if any.</summary>
    public AudioOutputHandle? Output => _output;

    internal Task? PumpTask => _pumpTask;

    public StreamingPlayback(HttpClient httpClient, ILogger logger, Func<ISampleProvider, AudioOutputHandle> openOutput)
        : this(
            httpClient,
            logger,
            openOutput,
            format => new AcmMp3FrameDecompressor(format),
            url => new MediaFoundationReader(url),
            stream => new StreamMediaFoundationReader(new MediaFoundationSeekableStream(stream, logger)),
            TimeSpan.FromSeconds(2))
    {
    }

    internal StreamingPlayback(
        HttpClient httpClient,
        ILogger logger,
        Func<ISampleProvider, AudioOutputHandle> openOutput,
        Func<WaveFormat, IMp3FrameDecompressor> createMp3Decompressor,
        Func<string, IWaveProvider> openMediaFoundationUrl,
        Func<Stream, IWaveProvider> openMediaFoundationStream,
        TimeSpan stopTimeout)
    {
        _httpClient = httpClient;
        _logger = logger;
        _openOutput = openOutput;
        _createMp3Decompressor = createMp3Decompressor;
        _openMediaFoundationUrl = openMediaFoundationUrl;
        _openMediaFoundationStream = openMediaFoundationStream;
        _stopTimeout = stopTimeout;
    }

    public async Task StartAsync(RadioStationDto station, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            CancellationTokenSource attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopCts.Token);
            CancellationToken token = attemptCts.Token;
            IcyStreamHandler? icy = null;
            IWaveProvider? decoded = null;
            IMp3FrameDecompressor? mp3Decompressor = null;
            bool published = false;

            try
            {
                icy = new IcyStreamHandler(_httpClient, _logger);
                icy.MetadataChanged += (_, title) => MetadataChanged?.Invoke(this, title);

                await icy.ConnectAsync(station.StreamUrl, token);

                string contentType = icy.ContentType?.ToLowerInvariant() ?? string.Empty;
                bool isMp3 = contentType is "audio/mpeg" or "";
                bool isAac = contentType is "audio/aac" or "audio/aacp" or "audio/x-aac";
                BufferedWaveProvider buffer;
                Func<Task> pump;

                if (isAac)
                {
                    // StreamMediaFoundationReader can't handle live ADTS over a
                    // non-seekable HTTP stream. Close the ICY connection we just
                    // opened (we lose stream-title metadata for AAC in exchange)
                    // and let Media Foundation use its own networking stack via
                    // the URL constructor — MF's HTTP source knows about live
                    // sources and doesn't try to scan the whole file.
                    icy.Dispose();
                    icy = null;

                    IWaveProvider aacDecoded = _openMediaFoundationUrl(station.StreamUrl);
                    decoded = aacDecoded;

                    BufferedWaveProvider aacBuffer = new(aacDecoded.WaveFormat, TimeSpan.FromSeconds(BufferDurationSeconds))
                    {
                        DiscardOnBufferOverflow = true
                    };

                    buffer = aacBuffer;
                    pump = () => PumpAsync(aacBuffer, aacDecoded, token);
                }
                else if (isMp3)
                {
                    Stream mp3Source = new ReadFullyStream(icy.AudioStream);

                    Mp3Frame? firstFrame = Mp3Frame.LoadFromStream(mp3Source);
                    if (firstFrame is null)
                        throw new InvalidOperationException("Stream did not yield a single MP3 frame");

                    WaveFormat mp3WaveFormat = new Mp3WaveFormat(
                        firstFrame.SampleRate,
                        firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                        firstFrame.FrameLength,
                        firstFrame.BitRate);

                    IMp3FrameDecompressor decompressor = _createMp3Decompressor(mp3WaveFormat);
                    mp3Decompressor = decompressor;

                    BufferedWaveProvider mp3Buffer = new(decompressor.OutputFormat, TimeSpan.FromSeconds(BufferDurationSeconds))
                    {
                        DiscardOnBufferOverflow = true
                    };

                    byte[] firstDecodeBuffer = new byte[16384];
                    int firstDecoded = decompressor.DecompressFrame(firstFrame, firstDecodeBuffer, 0);
                    mp3Buffer.AddSamples(firstDecodeBuffer, 0, firstDecoded);

                    buffer = mp3Buffer;
                    pump = () => PumpMp3Async(mp3Buffer, decompressor, mp3Source, token);
                }
                else
                {
                    // AAC / other: use StreamMediaFoundationReader. MediaFoundation
                    // seeks around while sniffing the container header, so wrap the
                    // non-seekable ICY stream in a buffer that supports rewinds
                    // within the probe region.
                    IWaveProvider otherDecoded = _openMediaFoundationStream(icy.AudioStream);
                    decoded = otherDecoded;

                    BufferedWaveProvider otherBuffer = new(otherDecoded.WaveFormat, TimeSpan.FromSeconds(BufferDurationSeconds))
                    {
                        DiscardOnBufferOverflow = true
                    };

                    buffer = otherBuffer;
                    pump = () => PumpAsync(otherBuffer, otherDecoded, token);
                }

                if (!TryPublish(icy, decoded, mp3Decompressor, buffer, attemptCts, pump, out Task<Task>? pumpStart))
                {
                    ReleaseLocals(icy, decoded, mp3Decompressor, attemptCts);
                    return;
                }

                published = true;
                SetBuffering(true);
                pumpStart.Start(TaskScheduler.Default);
                return;
            }
            catch (Exception ex)
            {
                if (!published)
                    ReleaseLocals(icy, decoded, mp3Decompressor, null);

                if (_stopCts.IsCancellationRequested)
                {
                    attemptCts.Dispose();
                    return;
                }

                lastError = ex;
                _logger.LogWarning(ex, "Stream connect attempt {Attempt} failed for {Url}", attempt, station.StreamUrl);

                try
                {
                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt), token);
                }
                catch (OperationCanceledException) when (_stopCts.IsCancellationRequested)
                {
                    return;
                }
                finally
                {
                    attemptCts.Dispose();
                }
            }
        }

        if (_stopCts.IsCancellationRequested)
            return;

        _logger.LogError(lastError, "Stream {Url} failed after 3 attempts", station.StreamUrl);
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        IcyStreamHandler? icy;
        Task? pumpTask;

        _stopCts.Cancel();

        lock (_outputLock)
        {
            cts = _cts;
            icy = _icy;
            pumpTask = _pumpTask;

            try { cts?.Cancel(); }
            catch (ObjectDisposedException) { }

            ReleaseOutput();

            _volumeProvider = null;
            _decoded = null;
            _mp3Decompressor = null;
            _buffer = null;
            _icy = null;
            _cts = null;
            _pumpTask = null;
        }

        icy?.Dispose();

        if (pumpTask is not null)
        {
            try
            {
                pumpTask.Wait(_stopTimeout);
            }
            catch (AggregateException)
            {
            }
        }

        if (pumpTask is null || pumpTask.IsCompleted)
            cts?.Dispose();
    }

    public void Pause() => _output?.Player.Pause();

    /// <summary>Resumes playback, reopening the output first when it was released.</summary>
    public void Resume()
    {
        lock (_outputLock)
        {
            if (_output is null && _buffer is not null)
                OpenOutput();

            _output?.Player.Play();
        }
    }

    public void SetVolume(double percent)
    {
        _volume = (float)Math.Clamp(percent / 100.0, 0.0, 1.0);

        if (_volumeProvider is not null)
            _volumeProvider.Volume = _volume;
    }

    /// <summary>Replaces the output on the current target while keeping the buffered stream: no reconnection.</summary>
    public void ReopenOutput(bool play)
    {
        lock (_outputLock)
        {
            if (_buffer is null)
                return;

            ReleaseOutput();

            if (!play)
                return;

            OpenOutput();
            _output?.Player.Play();
        }
    }

    private void OpenOutput()
    {
        BufferedWaveProvider buffer = _buffer ?? throw new InvalidOperationException("The stream buffer is not ready.");
        VolumeSampleProvider volumeProvider = new(buffer.ToSampleProvider()) { Volume = _volume };
        AudioOutputHandle output = _openOutput(volumeProvider);

        output.Player.PlaybackStopped += Output_PlaybackStopped;
        _volumeProvider = volumeProvider;
        _output = output;
    }

    private void ReleaseOutput()
    {
        AudioOutputHandle? output = _output;
        _output = null;

        if (output is null)
            return;

        output.Player.PlaybackStopped -= Output_PlaybackStopped;
        output.Dispose();
    }

    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is null || sender is not IWavePlayer player)
            return;

        _logger.LogWarning(e.Exception, "Radio output lost");

        ThreadPool.UnsafeQueueUserWorkItem(state => state.Playback.HandleOutputLost(state.Player), (Playback: this, Player: player), preferLocal: false);
    }

    private void HandleOutputLost(IWavePlayer player)
    {
        AudioOutputHandle? lost;

        lock (_outputLock)
        {
            lost = _output;

            if (lost is null || !ReferenceEquals(lost.Player, player))
                return;

            ReleaseOutput();
        }

        OutputLost?.Invoke(this, lost);
    }

    private async Task PumpMp3Async(
        BufferedWaveProvider buffer,
        IMp3FrameDecompressor decompressor,
        Stream mp3Source,
        CancellationToken ct)
    {
        try
        {
            byte[] decodeBuffer = new byte[16384];
            double bytesPerSecond = decompressor.OutputFormat.AverageBytesPerSecond;
            Stopwatch dryStopwatch = new();

            while (!ct.IsCancellationRequested
                   && buffer.BufferedBytes < bytesPerSecond * PreBufferSeconds)
            {
                Mp3Frame? frame = Mp3Frame.LoadFromStream(mp3Source);

                if (ct.IsCancellationRequested)
                    return;

                if (frame is null) { await Task.Delay(50, ct); continue; }

                int decoded = decompressor.DecompressFrame(frame, decodeBuffer, 0);
                buffer.AddSamples(decodeBuffer, 0, decoded);
            }

            if (ct.IsCancellationRequested)
                return;

            SetBuffering(false);
            _output?.Player.Play();

            while (!ct.IsCancellationRequested)
            {
                Mp3Frame? frame = null;
                try { frame = Mp3Frame.LoadFromStream(mp3Source); }
                catch (Exception ex) when (ex is EndOfStreamException or IOException) { }

                if (ct.IsCancellationRequested)
                    return;

                if (frame is not null)
                {
                    int decoded = decompressor.DecompressFrame(frame, decodeBuffer, 0);
                    buffer.AddSamples(decodeBuffer, 0, decoded);
                    dryStopwatch.Reset();
                }
                else
                {
                    if (!dryStopwatch.IsRunning) dryStopwatch.Start();

                    if (dryStopwatch.Elapsed.TotalSeconds >= TerminalNoBytesSeconds)
                    {
                        PlaybackEnded?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    await Task.Delay(100, ct);
                }

                double bufferedSec = buffer.BufferedBytes / bytesPerSecond;

                if (!IsBuffering && bufferedSec < BufferingTriggerSeconds)
                    SetBuffering(true);
                else if (IsBuffering && bufferedSec >= ResumeAfterUnderflowSeconds)
                    SetBuffering(false);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            decompressor.Dispose();
        }
    }

    private async Task PumpAsync(BufferedWaveProvider buffer, IWaveProvider decoded, CancellationToken ct)
    {
        try
        {
            byte[] readBuffer = new byte[8192];
            double bytesPerSecond = decoded.WaveFormat.AverageBytesPerSecond;
            Stopwatch dryStopwatch = new();

            while (!ct.IsCancellationRequested
                   && buffer.BufferedBytes < bytesPerSecond * PreBufferSeconds)
            {
                int read = decoded.Read(readBuffer);

                if (ct.IsCancellationRequested)
                    return;

                if (read <= 0) { await Task.Delay(50, ct); continue; }

                buffer.AddSamples(readBuffer, 0, read);
            }

            if (ct.IsCancellationRequested)
                return;

            SetBuffering(false);
            _output?.Player.Play();

            while (!ct.IsCancellationRequested)
            {
                int read = 0;

                try
                {
                    read = decoded.Read(readBuffer);
                }
                catch (IOException ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "AAC stream read failed; ending playback");
                    PlaybackEnded?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (ct.IsCancellationRequested)
                    return;

                if (read > 0)
                {
                    buffer.AddSamples(readBuffer, 0, read);
                    dryStopwatch.Reset();
                }
                else
                {
                    if (!dryStopwatch.IsRunning) dryStopwatch.Start();

                    if (dryStopwatch.Elapsed.TotalSeconds >= TerminalNoBytesSeconds)
                    {
                        PlaybackEnded?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    await Task.Delay(100, ct);
                }

                double bufferedSec = buffer.BufferedBytes / bytesPerSecond;

                // During underflow we surface IsBuffering=true to the UI but leave _output
                // playing. The output will naturally output silence while the buffer is
                // drained and resume as soon as new samples arrive; pausing/resuming the
                // output device would add clock drift and pop artifacts.
                if (!IsBuffering && bufferedSec < BufferingTriggerSeconds)
                    SetBuffering(true);
                else if (IsBuffering && bufferedSec >= ResumeAfterUnderflowSeconds)
                    SetBuffering(false);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            (decoded as IDisposable)?.Dispose();
        }
    }

    private void SetBuffering(bool value)
    {
        if (value == IsBuffering) return;
        IsBuffering = value;
        BufferingChanged?.Invoke(this, value);
    }

    private bool TryPublish(
        IcyStreamHandler? icy,
        IWaveProvider? decoded,
        IMp3FrameDecompressor? mp3Decompressor,
        BufferedWaveProvider buffer,
        CancellationTokenSource cts,
        Func<Task> pump,
        [NotNullWhen(true)] out Task<Task>? pumpStart)
    {
        lock (_outputLock)
        {
            if (_stopCts.IsCancellationRequested)
            {
                pumpStart = null;
                return false;
            }

            _buffer = buffer;

            try
            {
                OpenOutput();
            }
            catch
            {
                _buffer = null;
                throw;
            }

            _icy = icy;
            _decoded = decoded;
            _mp3Decompressor = mp3Decompressor;
            _cts = cts;

            pumpStart = new Task<Task>(pump);
            _pumpTask = pumpStart.Unwrap();

            return true;
        }
    }

    private static void ReleaseLocals(
        IcyStreamHandler? icy,
        IWaveProvider? decoded,
        IMp3FrameDecompressor? mp3Decompressor,
        CancellationTokenSource? cts)
    {
        (decoded as IDisposable)?.Dispose();
        mp3Decompressor?.Dispose();
        icy?.Dispose();
        cts?.Dispose();
    }

    public void Dispose() => Stop();
}