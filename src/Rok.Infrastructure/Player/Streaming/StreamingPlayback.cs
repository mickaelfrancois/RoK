using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.Compression;
using Rok.Application.Dto;

namespace Rok.Infrastructure.Player.Streaming;

internal sealed class StreamingPlayback : IDisposable
{
    public event EventHandler<string>? MetadataChanged;
    public event EventHandler? PlaybackEnded;
    public event EventHandler<bool>? BufferingChanged;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    private IcyStreamHandler? _icy;
    private IWaveProvider? _decoded;
    private BufferedWaveProvider? _buffer;
    private WaveOutEvent? _output;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private AcmMp3FrameDecompressor? _mp3Decompressor;

    private const double BufferDurationSeconds = 15.0;
    private const double PreBufferSeconds = 3.0;
    private const double ResumeAfterUnderflowSeconds = 2.0;
    private const double BufferingTriggerSeconds = 0.5;
    private const double TerminalNoBytesSeconds = 5.0;

    public bool IsBuffering { get; private set; }

    public StreamingPlayback(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task StartAsync(RadioStationDto station, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _icy = new IcyStreamHandler(_httpClient, _logger);
                _icy.MetadataChanged += (_, title) => MetadataChanged?.Invoke(this, title);

                await _icy.ConnectAsync(station.StreamUrl, _cts.Token);

                string contentType = _icy.ContentType?.ToLowerInvariant() ?? string.Empty;
                bool isMp3 = contentType is "audio/mpeg" or "";
                bool isAac = contentType is "audio/aac" or "audio/aacp" or "audio/x-aac";

                if (isAac)
                {
                    // StreamMediaFoundationReader can't handle live ADTS over a
                    // non-seekable HTTP stream. Close the ICY connection we just
                    // opened (we lose stream-title metadata for AAC in exchange)
                    // and let Media Foundation use its own networking stack via
                    // the URL constructor — MF's HTTP source knows about live
                    // sources and doesn't try to scan the whole file.
                    _icy.Dispose();
                    _icy = null;

                    _decoded = new MediaFoundationReader(station.StreamUrl);

                    _buffer = new BufferedWaveProvider(_decoded.WaveFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(BufferDurationSeconds),
                        DiscardOnBufferOverflow = true
                    };

                    _output = new WaveOutEvent();
                    _output.Init(_buffer);

                    SetBuffering(true);
                    _pumpTask = Task.Run(() => PumpAsync(_cts.Token), _cts.Token);
                    return;
                }

                if (isMp3)
                {
                    Stream mp3Source = new ReadFullyStream(_icy.AudioStream);

                    Mp3Frame? firstFrame = Mp3Frame.LoadFromStream(mp3Source);
                    if (firstFrame is null)
                        throw new InvalidOperationException("Stream did not yield a single MP3 frame");

                    WaveFormat mp3WaveFormat = new Mp3WaveFormat(
                        firstFrame.SampleRate,
                        firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                        firstFrame.FrameLength,
                        firstFrame.BitRate);

                    _mp3Decompressor = new AcmMp3FrameDecompressor(mp3WaveFormat);
                    _buffer = new BufferedWaveProvider(_mp3Decompressor.OutputFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(BufferDurationSeconds),
                        DiscardOnBufferOverflow = true
                    };

                    byte[] firstDecodeBuffer = new byte[16384];
                    int firstDecoded = _mp3Decompressor.DecompressFrame(firstFrame, firstDecodeBuffer, 0);
                    _buffer.AddSamples(firstDecodeBuffer, 0, firstDecoded);

                    _output = new WaveOutEvent();
                    _output.Init(_buffer);

                    SetBuffering(true);
                    _pumpTask = Task.Run(() => PumpMp3Async(mp3Source, _cts.Token), _cts.Token);
                    return;
                }

                // AAC / other: use StreamMediaFoundationReader. MediaFoundation
                // seeks around while sniffing the container header, so wrap the
                // non-seekable ICY stream in a buffer that supports rewinds
                // within the probe region.
                Stream mfSource = new MediaFoundationSeekableStream(_icy.AudioStream, _logger);
                _decoded = new StreamMediaFoundationReader(mfSource);

                _buffer = new BufferedWaveProvider(_decoded.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(BufferDurationSeconds),
                    DiscardOnBufferOverflow = true
                };

                _output = new WaveOutEvent();
                _output.Init(_buffer);

                SetBuffering(true);
                _pumpTask = Task.Run(() => PumpAsync(_cts.Token), _cts.Token);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Stream connect attempt {Attempt} failed for {Url}", attempt, station.StreamUrl);
                DisposeResources();

                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }

        _logger.LogError(lastError, "Stream {Url} failed after 3 attempts", station.StreamUrl);
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _pumpTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { /* pump cancelled */ }
        DisposeResources();
    }

    public void Pause() => _output?.Pause();

    public void Resume() => _output?.Play();

    public void SetVolume(double percent)
    {
        if (_output is null) return;
        _output.Volume = (float)Math.Clamp(percent / 100.0, 0.0, 1.0);
    }

    private async Task PumpMp3Async(Stream mp3Source, CancellationToken ct)
    {
        if (_buffer is null || _output is null || _mp3Decompressor is null)
            return;

        byte[] decodeBuffer = new byte[16384];
        double bytesPerSecond = _mp3Decompressor.OutputFormat.AverageBytesPerSecond;
        Stopwatch dryStopwatch = new();

        // Pre-buffer
        while (!ct.IsCancellationRequested
               && _buffer.BufferedBytes < bytesPerSecond * PreBufferSeconds)
        {
            Mp3Frame? frame = Mp3Frame.LoadFromStream(mp3Source);
            if (frame is null) { await Task.Delay(50, ct); continue; }
            int decoded = _mp3Decompressor.DecompressFrame(frame, decodeBuffer, 0);
            _buffer.AddSamples(decodeBuffer, 0, decoded);
        }

        SetBuffering(false);
        _output.Play();

        while (!ct.IsCancellationRequested)
        {
            Mp3Frame? frame = null;
            try { frame = Mp3Frame.LoadFromStream(mp3Source); }
            catch (Exception ex) when (ex is EndOfStreamException or IOException) { }

            if (frame is not null)
            {
                int decoded = _mp3Decompressor.DecompressFrame(frame, decodeBuffer, 0);
                _buffer.AddSamples(decodeBuffer, 0, decoded);
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

            double bufferedSec = _buffer.BufferedBytes / bytesPerSecond;

            if (!IsBuffering && bufferedSec < BufferingTriggerSeconds)
                SetBuffering(true);
            else if (IsBuffering && bufferedSec >= ResumeAfterUnderflowSeconds)
                SetBuffering(false);
        }
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        if (_decoded is null || _buffer is null || _output is null)
            return;

        byte[] readBuffer = new byte[8192];
        double bytesPerSecond = _decoded.WaveFormat.AverageBytesPerSecond;
        Stopwatch dryStopwatch = new();

        // Pre-buffer
        while (!ct.IsCancellationRequested
               && _buffer.BufferedBytes < bytesPerSecond * PreBufferSeconds)
        {
            int read = _decoded.Read(readBuffer, 0, readBuffer.Length);
            if (read <= 0) { await Task.Delay(50, ct); continue; }
            _buffer.AddSamples(readBuffer, 0, read);
        }

        SetBuffering(false);
        _output.Play();

        while (!ct.IsCancellationRequested)
        {
            int read = 0;

            try
            {
                read = _decoded.Read(readBuffer, 0, readBuffer.Length);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "AAC stream read failed; ending playback");
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (read > 0)
            {
                _buffer.AddSamples(readBuffer, 0, read);
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

            double bufferedSec = _buffer.BufferedBytes / bytesPerSecond;

            // During underflow we surface IsBuffering=true to the UI but leave _output
            // playing. WaveOutEvent will naturally output silence while the buffer is
            // drained and resume as soon as new samples arrive; pausing/resuming the
            // output device would add clock drift and pop artifacts.
            if (!IsBuffering && bufferedSec < BufferingTriggerSeconds)
                SetBuffering(true);
            else if (IsBuffering && bufferedSec >= ResumeAfterUnderflowSeconds)
                SetBuffering(false);
        }
    }

    private void SetBuffering(bool value)
    {
        if (value == IsBuffering) return;
        IsBuffering = value;
        BufferingChanged?.Invoke(this, value);
    }

    private void DisposeResources()
    {
        try { _output?.Stop(); } catch { }
        _output?.Dispose(); _output = null;
        (_decoded as IDisposable)?.Dispose(); _decoded = null;
        _mp3Decompressor?.Dispose(); _mp3Decompressor = null;
        _buffer = null;
        _icy?.Dispose(); _icy = null;
        _cts?.Dispose(); _cts = null;
    }

    public void Dispose() => DisposeResources();
}
