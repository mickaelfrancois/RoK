using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Rok.Application.Dto;
using Rok.Application.Player.Output;
using Rok.Infrastructure.Player.Output;
using Rok.Infrastructure.Player.Streaming;

namespace Rok.Infrastructure.UnitTests.Player.Streaming;

public sealed class StreamingPlaybackTests : IDisposable
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShortStopTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongStopTimeout = TimeSpan.FromSeconds(3);

    private static readonly RadioStationDto Station = new(
        1, "Test radio", "http://radio.test/stream", null, null, null, null, null, null, DateTime.UtcNow, null);

    private readonly CancellationTokenSource _testCts = new();
    private readonly RecordingLogger _logger = new();
    private readonly List<GatedStream> _streams = [];
    private readonly List<GatedWaveProvider> _providers = [];
    private readonly List<StreamingPlayback> _playbacks = [];
    private FakeMp3Decompressor? _decoder;
    private int _outputsOpened;
    private int _endedRaised;

    public void Dispose()
    {
        _testCts.Cancel();

        foreach (GatedStream stream in _streams)
            stream.OpenGate();

        foreach (GatedWaveProvider provider in _providers)
            provider.OpenGate();

        foreach (StreamingPlayback playback in _playbacks)
        {
            _ = Record.Exception(playback.Dispose);
        }

        _testCts.Dispose();
    }

    [Fact(DisplayName = "mp3_frame_builder_yields_a_frame_naudio_can_parse")]
    public void Mp3FrameBuilder_YieldsParsableFrame()
    {
        // Arrange
        using MemoryStream stream = new(Mp3FrameBuilder.Frames(1));

        // Act
        Mp3Frame? frame = Mp3Frame.LoadFromStream(stream);

        // Assert
        Assert.NotNull(frame);
        Assert.Equal(44100, frame.SampleRate);
        Assert.Equal(Mp3FrameBuilder.FrameLength, frame.FrameLength);
    }

    [Fact(DisplayName = "stop_while_mp3_pump_blocked_in_steady_loop_leaves_no_faulted_task")]
    public async Task Stop_Mp3PumpBlockedInSteadyLoop_LeavesNoFaultedTask()
    {
        // Arrange
        GatedStream body = NewStream(Mp3FrameBuilder.Frames(4), Mp3FrameBuilder.Frames(2), disposeReleasesGate: false);
        StreamingPlayback sut = CreateSut(StubHttpHandler.Serving(body, "audio/mpeg"), ShortStopTimeout);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await body.Blocked.WaitAsync(Guard);

        // Act
        await StopAsync(sut);
        body.OpenGate();

        // Assert
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertDecoderDisposedOnce();
    }

    [Fact(DisplayName = "stop_while_mp3_pump_blocked_in_prebuffer_leaves_no_faulted_task")]
    public async Task Stop_Mp3PumpBlockedInPrebuffer_LeavesNoFaultedTask()
    {
        // Arrange
        GatedStream body = NewStream(Mp3FrameBuilder.Frames(1), Mp3FrameBuilder.Frames(2), disposeReleasesGate: false);
        StreamingPlayback sut = CreateSut(StubHttpHandler.Serving(body, "audio/mpeg"), ShortStopTimeout);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await body.Blocked.WaitAsync(Guard);

        // Act
        await StopAsync(sut);
        body.OpenGate();

        // Assert
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertDecoderDisposedOnce();
    }

    [Fact(DisplayName = "stop_unblocks_mp3_read_by_disposing_the_source")]
    public async Task Stop_Mp3PumpBlocked_DisposesSourceToUnblockRead()
    {
        // Arrange
        GatedStream body = NewStream(Mp3FrameBuilder.Frames(4), [], disposeReleasesGate: true);
        StreamingPlayback sut = CreateSut(StubHttpHandler.Serving(body, "audio/mpeg"), LongStopTimeout);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await body.Blocked.WaitAsync(Guard);

        // Act
        TimeSpan elapsed = await StopAsync(sut);

        // Assert
        Assert.True(elapsed < TimeSpan.FromSeconds(1.5), $"Stop took {elapsed.TotalMilliseconds:F0} ms; the blocked read was not released by disposing the source.");
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertDecoderDisposedOnce();
    }

    [Fact(DisplayName = "stop_unblocks_icy_mp3_read_by_disposing_the_source")]
    public async Task Stop_IcyMp3PumpBlocked_DisposesSourceToUnblockRead()
    {
        // Arrange
        GatedStream body = NewStream(Mp3FrameBuilder.Frames(4), [], disposeReleasesGate: true);
        StreamingPlayback sut = CreateSut(StubHttpHandler.Serving(body, "audio/mpeg", icyMetaInt: 8192), LongStopTimeout);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await body.Blocked.WaitAsync(Guard);

        // Act
        TimeSpan elapsed = await StopAsync(sut);

        // Assert
        Assert.True(elapsed < TimeSpan.FromSeconds(1.5), $"Stop took {elapsed.TotalMilliseconds:F0} ms; the ICY stream did not dispose its source.");
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertDecoderDisposedOnce();
    }

    [Fact(DisplayName = "stop_while_aac_pump_blocked_leaves_no_faulted_task")]
    public async Task Stop_AacPumpBlocked_LeavesNoFaultedTask()
    {
        // Arrange
        GatedWaveProvider reader = NewProvider(initialBytes: 6 * 8192, afterGateBytes: 8192);
        StubHttpHandler handler = StubHttpHandler.Serving(NewStream([], [], disposeReleasesGate: false), "audio/aac");
        StreamingPlayback sut = CreateSut(handler, ShortStopTimeout, openUrl: _ => reader);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await reader.Blocked.WaitAsync(Guard);

        // Act
        await StopAsync(sut);
        reader.OpenGate();

        // Assert
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertReaderDisposedOnce(reader);
    }

    [Fact(DisplayName = "stop_while_mf_stream_pump_blocked_leaves_no_faulted_task")]
    public async Task Stop_MediaFoundationStreamPumpBlocked_LeavesNoFaultedTask()
    {
        // Arrange
        GatedWaveProvider reader = NewProvider(initialBytes: 6 * 8192, afterGateBytes: 8192);
        StubHttpHandler handler = StubHttpHandler.Serving(NewStream([], [], disposeReleasesGate: false), "audio/ogg");
        StreamingPlayback sut = CreateSut(handler, ShortStopTimeout, openStream: _ => reader);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await reader.Blocked.WaitAsync(Guard);

        // Act
        await StopAsync(sut);
        reader.OpenGate();

        // Assert
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertReaderDisposedOnce(reader);
    }

    [Fact(DisplayName = "stop_during_connect_does_not_retry_nor_report")]
    public async Task Stop_DuringConnect_DoesNotRetryNorReport()
    {
        // Arrange
        StubHttpHandler handler = StubHttpHandler.BlockingUntilCancelled();
        StreamingPlayback sut = CreateSut(handler, ShortStopTimeout);
        Task start = sut.StartAsync(Station, _testCts.Token);
        await handler.Entered.WaitAsync(Guard);

        // Act
        await StopAsync(sut);
        bool completed = await CompletesWithinAsync(start, TimeSpan.FromSeconds(3));

        // Assert
        Assert.True(completed, "StartAsync did not complete after Stop: it kept connecting.");
        await start;
        Assert.Equal(1, handler.Calls);
        Assert.Equal(0, _outputsOpened);
        Assert.Equal(0, _endedRaised);
        AssertNoWarning();
    }

    [Fact(DisplayName = "stop_during_retry_delay_does_not_retry_nor_report")]
    public async Task Stop_DuringRetryDelay_DoesNotRetryNorReport()
    {
        // Arrange
        StubHttpHandler handler = StubHttpHandler.Failing();
        StreamingPlayback sut = CreateSut(handler, ShortStopTimeout);
        Task start = sut.StartAsync(Station, _testCts.Token);
        await _logger.FirstWarning.WaitAsync(Guard);
        await Task.Delay(100);

        // Act
        await StopAsync(sut);
        bool completed = await CompletesWithinAsync(start, TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed, "StartAsync did not complete after Stop: the retry delay was not interrupted.");
        await start;
        Assert.Equal(1, handler.Calls);
        Assert.Equal(0, _endedRaised);
        Assert.DoesNotContain(_logger.Levels, level => level >= LogLevel.Error);
    }

    [Fact(DisplayName = "stop_and_dispose_are_idempotent")]
    public async Task StopAndDispose_AreIdempotent()
    {
        // Arrange
        GatedStream body = NewStream(Mp3FrameBuilder.Frames(4), Mp3FrameBuilder.Frames(2), disposeReleasesGate: false);
        StreamingPlayback sut = CreateSut(StubHttpHandler.Serving(body, "audio/mpeg"), ShortStopTimeout);
        StreamingPlayback unused = CreateSut(StubHttpHandler.Failing(), ShortStopTimeout);
        await StartAsync(sut);
        Task? pump = sut.PumpTask;
        await body.Blocked.WaitAsync(Guard);

        // Act
        Exception? failure = Record.Exception(() =>
        {
            sut.Stop();
            sut.Stop();
            sut.Dispose();
            unused.Dispose();
        });
        body.OpenGate();

        // Assert
        Assert.Null(failure);
        await AssertPumpEndedCleanlyAsync(pump);
        AssertQuiet();
        AssertDecoderDisposedOnce();
    }

    private StreamingPlayback CreateSut(
        StubHttpHandler handler,
        TimeSpan stopTimeout,
        Func<string, IWaveProvider>? openUrl = null,
        Func<Stream, IWaveProvider>? openStream = null)
    {
        HttpClient client = new(handler);

        StreamingPlayback playback = new(
            client,
            _logger,
            _ =>
            {
                Interlocked.Increment(ref _outputsOpened);
                return new AudioOutputHandle(new FakeWavePlayer(), "device", true, EAudioOutputMode.Shared, null, null);
            },
            _ =>
            {
                _decoder = new FakeMp3Decompressor();
                return _decoder;
            },
            openUrl ?? (_ => throw new InvalidOperationException("Media Foundation URL reader not expected.")),
            openStream ?? (_ => throw new InvalidOperationException("Media Foundation stream reader not expected.")),
            stopTimeout);

        playback.PlaybackEnded += (_, _) => Interlocked.Increment(ref _endedRaised);
        _playbacks.Add(playback);

        return playback;
    }

    private GatedStream NewStream(byte[] initial, byte[] afterGate, bool disposeReleasesGate)
    {
        GatedStream stream = new(initial, afterGate, disposeReleasesGate);
        _streams.Add(stream);

        return stream;
    }

    private GatedWaveProvider NewProvider(int initialBytes, int afterGateBytes)
    {
        GatedWaveProvider provider = new(initialBytes, afterGateBytes);
        _providers.Add(provider);

        return provider;
    }

#pragma warning disable VSTHRD003
    private async Task StartAsync(StreamingPlayback sut)
    {
        Task start = sut.StartAsync(Station, _testCts.Token);
        bool completed = await CompletesWithinAsync(start, Guard);

        Assert.True(completed, "StartAsync did not complete.");
        await start;
    }

    private static async Task<TimeSpan> StopAsync(StreamingPlayback sut)
    {
        Stopwatch watch = Stopwatch.StartNew();
        Task stop = Task.Run(sut.Stop);
        bool completed = await CompletesWithinAsync(stop, TimeSpan.FromSeconds(10));

        Assert.True(completed, "Stop did not return.");
        await stop;

        return watch.Elapsed;
    }

    private static async Task<bool> CompletesWithinAsync(Task task, TimeSpan timeout)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(timeout));

        return ReferenceEquals(finished, task);
    }

    private async Task AssertPumpEndedCleanlyAsync(Task? pump)
    {
        if (pump is null)
            return;

        bool completed = await CompletesWithinAsync(pump, Guard);

        Assert.True(completed, "The pump task did not complete.");
        Assert.True(pump.Status != TaskStatus.Faulted, $"The pump task faulted: {pump.Exception?.GetBaseException()}");
    }

    private void AssertQuiet()
    {
        Assert.Equal(0, _endedRaised);
        AssertNoWarning();
    }

    private void AssertNoWarning() =>
        Assert.DoesNotContain(_logger.Levels, level => level >= LogLevel.Warning);

    private void AssertDecoderDisposedOnce()
    {
        FakeMp3Decompressor decoder = Assert.IsType<FakeMp3Decompressor>(_decoder);

        Assert.Equal(0, decoder.UseAfterDisposeCount);
        Assert.Equal(1, decoder.DisposeCount);
    }

    private static void AssertReaderDisposedOnce(GatedWaveProvider reader)
    {
        Assert.Equal(0, reader.UseAfterDisposeCount);
        Assert.Equal(1, reader.DisposeCount);
    }

#pragma warning restore VSTHRD003

    private static class Mp3FrameBuilder
    {
        public const int FrameLength = 417;

        public static byte[] Frames(int count)
        {
            byte[] data = new byte[FrameLength * count];

            for (int i = 0; i < count; i++)
            {
                int offset = i * FrameLength;
                data[offset] = 0xFF;
                data[offset + 1] = 0xFB;
                data[offset + 2] = 0x90;
                data[offset + 3] = 0x00;
            }

            return data;
        }
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        private StubHttpHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public int Calls => Volatile.Read(ref _calls);

        public Task Entered => _entered.Task;

        public static StubHttpHandler Serving(Stream body, string contentType, int? icyMetaInt = null) =>
            new(_ =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(body) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                if (icyMetaInt is not null)
                    response.Headers.TryAddWithoutValidation("icy-metaint", icyMetaInt.Value.ToString());

                return Task.FromResult(response);
            });

        public static StubHttpHandler BlockingUntilCancelled() =>
            new(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);

                throw new InvalidOperationException("Unreachable.");
            });

        public static StubHttpHandler Failing() =>
            new(_ => throw new HttpRequestException("Connection refused."));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _entered.TrySetResult();

            return _respond(cancellationToken);
        }
    }

    private sealed class GatedStream : Stream
    {
        private readonly byte[] _initial;
        private readonly byte[] _afterGate;
        private readonly bool _disposeReleasesGate;
        private readonly ManualResetEventSlim _gate = new(false);
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _initialPosition;
        private int _afterGatePosition;
        private volatile bool _disposed;

        public GatedStream(byte[] initial, byte[] afterGate, bool disposeReleasesGate)
        {
            _initial = initial;
            _afterGate = afterGate;
            _disposeReleasesGate = disposeReleasesGate;
        }

        public Task Blocked => _blocked.Task;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public void OpenGate() => _gate.Set();

        private void ThrowIfDisposedAndStrict()
        {
            if (_disposeReleasesGate)
                ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposedAndStrict();

            if (_initialPosition < _initial.Length)
            {
                int n = Math.Min(count, _initial.Length - _initialPosition);
                Array.Copy(_initial, _initialPosition, buffer, offset, n);
                _initialPosition += n;

                return n;
            }

            _blocked.TrySetResult();
            _gate.Wait(TimeSpan.FromSeconds(30));

            ThrowIfDisposedAndStrict();

            if (_afterGatePosition < _afterGate.Length)
            {
                int n = Math.Min(count, _afterGate.Length - _afterGatePosition);
                Array.Copy(_afterGate, _afterGatePosition, buffer, offset, n);
                _afterGatePosition += n;

                return n;
            }

            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _disposed = true;

            if (disposing && _disposeReleasesGate)
                _gate.Set();

            base.Dispose(disposing);
        }
    }

    private sealed class FakeMp3Decompressor : IMp3FrameDecompressor
    {
        private int _disposeCount;
        private int _useAfterDisposeCount;

        public WaveFormat OutputFormat { get; } = new(8000, 16, 1);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public int UseAfterDisposeCount => Volatile.Read(ref _useAfterDisposeCount);

        public int DecompressFrame(Mp3Frame frame, byte[] dest, int destOffset)
        {
            if (DisposeCount > 0)
                Interlocked.Increment(ref _useAfterDisposeCount);

            Array.Clear(dest, destOffset, 16000);

            return 16000;
        }

        public void Reset()
        {
        }

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class GatedWaveProvider : IWaveProvider, IDisposable
    {
        private readonly ManualResetEventSlim _gate = new(false);
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _initialRemaining;
        private int _afterGateRemaining;
        private int _disposeCount;
        private int _useAfterDisposeCount;

        public GatedWaveProvider(int initialBytes, int afterGateBytes)
        {
            _initialRemaining = initialBytes;
            _afterGateRemaining = afterGateBytes;
        }

        public WaveFormat WaveFormat { get; } = new(8000, 16, 1);

        public Task Blocked => _blocked.Task;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public int UseAfterDisposeCount => Volatile.Read(ref _useAfterDisposeCount);

        public void OpenGate() => _gate.Set();

        public int Read(Span<byte> buffer)
        {
            if (DisposeCount > 0)
                Interlocked.Increment(ref _useAfterDisposeCount);

            int served;

            if (_initialRemaining > 0)
            {
                served = Math.Min(buffer.Length, _initialRemaining);
                _initialRemaining -= served;
                buffer[..served].Clear();

                return served;
            }

            _blocked.TrySetResult();
            _gate.Wait(TimeSpan.FromSeconds(30));

            if (DisposeCount > 0)
                Interlocked.Increment(ref _useAfterDisposeCount);

            served = Math.Min(buffer.Length, _afterGateRemaining);
            _afterGateRemaining -= served;
            buffer[..served].Clear();

            return served;
        }

        public void Dispose() => Interlocked.Increment(ref _disposeCount);
    }

    private sealed class FakeWavePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public WaveFormat OutputWaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public float Volume { get; set; } = 1f;

        public void Init(IWaveProvider waveProvider)
        {
        }

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Pause() => PlaybackState = PlaybackState.Paused;

        public void Stop()
        {
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly Lock _lock = new();
        private readonly List<LogLevel> _levels = [];
        private readonly TaskCompletionSource _firstWarning = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<LogLevel> Levels
        {
            get
            {
                lock (_lock)
                    return [.. _levels];
            }
        }

        public Task FirstWarning => _firstWarning.Task;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
                _levels.Add(logLevel);

            if (logLevel >= LogLevel.Warning)
                _firstWarning.TrySetResult();
        }
    }
}