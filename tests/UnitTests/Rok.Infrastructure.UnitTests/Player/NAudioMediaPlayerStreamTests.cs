using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Player.Output;
using Rok.Infrastructure.Player;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player;

public sealed class NAudioMediaPlayerStreamTests : IDisposable
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan QuietWindow = TimeSpan.FromMilliseconds(600);

    private static readonly RadioStationDto Station = new(
        1, "Test radio", "http://radio.test/stream", null, null, null, null, null, null, DateTime.UtcNow, null);

    private readonly Queue<BlockingHandler> _handlers = new();
    private readonly NAudioMediaPlayer _sut;
    private int _mediaChanged;
    private int _stateChanged;
    private int _mediaEnded;

    public NAudioMediaPlayerStreamTests()
    {
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("RadioStream")).Returns(() => new HttpClient(_handlers.Dequeue()));

        _sut = new NAudioMediaPlayer(
            NullLogger<NAudioMediaPlayer>.Instance,
            factory.Object,
            Mock.Of<IAudioOutputFactory>(),
            Mock.Of<IAudioFormatProbe>());

        _sut.OnMediaChanged += (_, _) => Interlocked.Increment(ref _mediaChanged);
        _sut.OnMediaStateChanged += (_, _) => Interlocked.Increment(ref _stateChanged);
        _sut.OnMediaEnded += (_, _) => Interlocked.Increment(ref _mediaEnded);
    }

    public void Dispose() => _sut.Dispose();

    [Fact(DisplayName = "stop_during_stream_start_raises_no_media_event")]
    public async Task Stop_DuringStreamStart_RaisesNoMediaEvent()
    {
        // Arrange
        BlockingHandler handler = Enqueue();
        _sut.SetStream(Station);
        await handler.Entered.WaitAsync(Guard);

        // Act
        _sut.Stop();
        ResetCounters();
        await handler.Cancelled.WaitAsync(Guard);
        await Task.Delay(QuietWindow);

        // Assert
        Assert.Equal(0, _mediaChanged);
        Assert.Equal(0, _stateChanged);
        Assert.Equal(0, _mediaEnded);
    }

    [Fact(DisplayName = "station_change_during_start_keeps_the_new_stream")]
    public async Task SetStream_DuringPreviousStart_KeepsTheNewStream()
    {
        // Arrange
        BlockingHandler first = Enqueue();
        BlockingHandler second = Enqueue();
        _sut.SetStream(Station);
        await first.Entered.WaitAsync(Guard);

        // Act
        _sut.SetStream(Station);
        ResetCounters();
        await second.Entered.WaitAsync(Guard);
        await first.Cancelled.WaitAsync(Guard);
        await Task.Delay(QuietWindow);

        // Assert
        Assert.Equal(0, _mediaChanged);
        Assert.Equal(0, _stateChanged);
        Assert.Equal(0, _mediaEnded);
        Assert.False(second.Cancelled.IsCompleted);
        Assert.True(_sut.IsLive);
    }

    private void ResetCounters()
    {
        Interlocked.Exchange(ref _mediaChanged, 0);
        Interlocked.Exchange(ref _stateChanged, 0);
        Interlocked.Exchange(ref _mediaEnded, 0);
    }

    private BlockingHandler Enqueue()
    {
        BlockingHandler handler = new();
        _handlers.Enqueue(handler);

        return handler;
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public Task Cancelled => _cancelled.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _entered.TrySetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _cancelled.TrySetResult();

                throw;
            }

            throw new InvalidOperationException("Unreachable.");
        }
    }
}