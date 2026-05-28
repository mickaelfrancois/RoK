using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
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
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _icy = new IcyStreamHandler(_httpClient, _logger);
                _icy.MetadataChanged += (_, title) => MetadataChanged?.Invoke(this, title);

                await _icy.ConnectAsync(station.StreamUrl, _cts.Token);

                _decoded = CreateDecoder(_icy.AudioStream, _icy.ContentType);

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
            catch (Exception ex) when (attempt < 3)
            {
                _logger.LogWarning(ex, "Stream connect attempt {Attempt} failed", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                DisposeResources();
            }
        }

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

    private static IWaveProvider CreateDecoder(Stream stream, string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "audio/aac" or "audio/aacp" or "audio/mp4" =>
                new StreamMediaFoundationReader(stream),
            _ => new Mp3FileReader(stream)
        };
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
            int read = _decoded.Read(readBuffer, 0, readBuffer.Length);

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
        _buffer = null;
        _icy?.Dispose(); _icy = null;
        _cts?.Dispose(); _cts = null;
    }

    public void Dispose() => DisposeResources();
}
