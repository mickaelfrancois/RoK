using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;
using Rok.Application.Dto;
using Rok.Application.Player.Output;
using Rok.Infrastructure.Player;
using Rok.Infrastructure.Player.Output;

namespace Rok.Infrastructure.UnitTests.Player;

public sealed class NAudioMediaPlayerOutputTests : IDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    private readonly FakeOutputFactory _factory = new();
    private readonly Mock<IAudioFormatProbe> _probe = new();
    private readonly TestWaveFile _first = TestWaveFile.CreateSilence(2);
    private readonly TestWaveFile _second = TestWaveFile.CreateSilence(2);
    private readonly NAudioMediaPlayer _sut;

    public NAudioMediaPlayerOutputTests()
    {
        _probe.Setup(p => p.GetBitsPerSample(It.IsAny<string>())).Returns(16);
        _sut = new NAudioMediaPlayer(NullLogger<NAudioMediaPlayer>.Instance, Mock.Of<IHttpClientFactory>(), _factory, _probe.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _first.Dispose();
        _second.Dispose();
    }

    private static TrackDto Track(long id, TestWaveFile file) => new() { Id = id, Title = $"t{id}", MusicFile = file.Path };

    private static Task<T> WaitAsync<T>(TaskCompletionSource<T> source) => source.Task.WaitAsync(EventTimeout);

    [Fact(DisplayName = "playback_stopped_with_exception_raises_output_lost_not_media_ended")]
    public async Task PlaybackStoppedWithException_RaisesOutputLost_NotMediaEnded()
    {
        // Arrange
        TaskCompletionSource<OutputLostEventArgs> lost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool mediaEnded = false;
        _sut.OnOutputLost += (_, e) => lost.TrySetResult(e);
        _sut.OnMediaEnded += (_, _) => mediaEnded = true;
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.Play();
        FakeWavePlayer player = _factory.LastPlayer;

        // Act
        player.RaiseStopped(new COMException("device invalidated", unchecked((int)0x88890004)));
        OutputLostEventArgs args = await WaitAsync(lost);

        // Assert
        Assert.True(args.WasPlaying);
        Assert.True(args.WasOnPreferred);
        Assert.False(mediaEnded);
        Assert.True(player.IsDisposed);
        Assert.False(_sut.OutputState.IsOpen);
    }

    [Fact(DisplayName = "playback_stopped_without_exception_raises_media_ended_off_the_render_thread")]
    public async Task PlaybackStoppedWithoutException_RaisesMediaEnded_OffTheEmittingThread()
    {
        // Arrange
        TaskCompletionSource<int> ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _sut.OnMediaEnded += (_, _) => ended.TrySetResult(Environment.CurrentManagedThreadId);
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.Play();
        int emittingThread = Environment.CurrentManagedThreadId;

        // Act
        _factory.LastPlayer.RaiseStopped(null);
        int handlingThread = await WaitAsync(ended);

        // Assert
        Assert.NotEqual(emittingThread, handlingThread);
    }

    [Fact(DisplayName = "set_output_target_while_playing_reopens_at_same_position")]
    public void SetOutputTarget_WhilePlaying_ReopensAtSamePosition()
    {
        // Arrange
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.Play();
        _sut.SetPosition(1);
        FakeWavePlayer previous = _factory.LastPlayer;
        AudioOutputTarget target = new("dac", EAudioOutputMode.Shared);

        // Act
        _sut.SetOutputTarget(target);

        // Assert
        Assert.Equal(2, _factory.Opened.Count);
        Assert.Equal(target, _factory.Opened[^1].Target);
        Assert.True(previous.IsDisposed);
        Assert.Equal(PlaybackState.Playing, _factory.LastPlayer.PlaybackState);
        Assert.Equal(1, _sut.Position, precision: 1);
    }

    [Theory(DisplayName = "late_playback_stopped_from_a_replaced_output_is_ignored")]
    [InlineData(false)]
    [InlineData(true)]
    public void LatePlaybackStopped_FromReplacedOutput_IsIgnored(bool withException)
    {
        // Arrange
        bool mediaEnded = false;
        bool outputLost = false;
        _sut.OnMediaEnded += (_, _) => mediaEnded = true;
        _sut.OnOutputLost += (_, _) => outputLost = true;
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.Play();
        FakeWavePlayer previous = _factory.LastPlayer;
        _sut.SetOutputTarget(new AudioOutputTarget("dac", EAudioOutputMode.Shared));
        FakeWavePlayer current = _factory.LastPlayer;

        // Act
        _sut.HandleOutputStopped(previous, withException ? new COMException("gone", unchecked((int)0x88890004)) : null);

        // Assert
        Assert.False(mediaEnded);
        Assert.False(outputLost);
        Assert.False(current.IsDisposed);
        Assert.True(_sut.OutputState.IsOpen);
    }

    [Fact(DisplayName = "set_output_target_while_paused_reopens_lazily_on_play")]
    public void SetOutputTarget_WhilePaused_ReopensLazilyOnPlay()
    {
        // Arrange
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.Play();
        _sut.Pause();
        FakeWavePlayer previous = _factory.LastPlayer;

        // Act
        _sut.SetOutputTarget(new AudioOutputTarget("dac", EAudioOutputMode.Shared));
        int opensWhilePaused = _factory.Opened.Count;
        _sut.Play();

        // Assert
        Assert.True(previous.IsDisposed);
        Assert.Equal(1, opensWhilePaused);
        Assert.Equal(2, _factory.Opened.Count);
        Assert.Equal(PlaybackState.Playing, _factory.LastPlayer.PlaybackState);
    }

    [Fact(DisplayName = "exclusive_fallback_is_reported_and_next_track_retries_exclusive")]
    public void ExclusiveFallback_IsReported_AndNextTrackRetriesExclusive()
    {
        // Arrange
        _factory.FallbackReason = EExclusiveFallbackReason.DeviceBusy;
        _sut.SetOutputTarget(new AudioOutputTarget(string.Empty, EAudioOutputMode.Exclusive));

        // Act
        _sut.SetTrack(Track(1, _first), 1f);
        EExclusiveFallbackReason? reported = _sut.OutputState.FallbackReason;
        _sut.SetTrack(Track(2, _second), 1f);

        // Assert
        Assert.Equal(EExclusiveFallbackReason.DeviceBusy, reported);
        Assert.Equal(2, _factory.Opened.Count);
        Assert.All(_factory.Opened, open => Assert.Equal(EAudioOutputMode.Exclusive, open.Target.Mode));
        Assert.Equal(16, _factory.Opened[^1].NativeBits);
    }

    [Fact(DisplayName = "queue_next_track_is_refused_while_in_shared_fallback")]
    public void QueueNextTrack_IsRefused_WhileInSharedFallback()
    {
        // Arrange
        _factory.FallbackReason = EExclusiveFallbackReason.FormatRefused;
        _sut.SetOutputTarget(new AudioOutputTarget(string.Empty, EAudioOutputMode.Exclusive));
        _sut.SetTrack(Track(1, _first), 1f);

        // Act
        bool queued = _sut.QueueNextTrack(Track(2, _second), 1f);

        // Assert
        Assert.False(queued);
    }

    [Theory(DisplayName = "queue_next_track_is_refused_when_exclusive_format_cannot_chain")]
    [InlineData(24, false)]
    [InlineData(16, true)]
    public void QueueNextTrack_FollowsExclusiveChainingRule(int nextBits, bool expected)
    {
        // Arrange
        _factory.ExclusiveFormat = ExclusiveFormatLadder.Candidates(44100, 2, 16)[0];
        _sut.SetOutputTarget(new AudioOutputTarget(string.Empty, EAudioOutputMode.Exclusive));
        _sut.SetTrack(Track(1, _first), 1f);
        _probe.Setup(p => p.GetBitsPerSample(_second.Path)).Returns(nextBits);

        // Act
        bool queued = _sut.QueueNextTrack(Track(2, _second), 1f);

        // Assert
        Assert.Equal(expected, queued);
    }

    [Fact(DisplayName = "processing_neutrality_flip_raises_output_state_changed")]
    public void NeutralityFlip_RaisesOutputStateChanged_OncePerFlip()
    {
        // Arrange
        _sut.SetTrack(Track(1, _first), 1f);
        _sut.SetVolume(100);
        List<AudioOutputState> states = [];
        _sut.OnOutputStateChanged += (_, state) => states.Add(state);

        // Act
        _sut.SetVolume(50);
        _sut.SetVolume(40);
        _sut.SetVolume(100);

        // Assert
        Assert.Equal([false, true], states.Select(state => state.IsProcessingNeutral));
    }

    private sealed class FakeOutputFactory : IAudioOutputFactory
    {
        public List<(AudioOutputTarget Target, int NativeBits)> Opened { get; } = [];

        public EExclusiveFallbackReason? FallbackReason { get; set; }

        public WaveFormat? ExclusiveFormat { get; set; }

        public FakeWavePlayer LastPlayer { get; private set; } = null!;

        public AudioOutputHandle Open(AudioOutputTarget target, ISampleProvider source, int nativeBitsPerSample)
        {
            Opened.Add((target, nativeBitsPerSample));
            LastPlayer = new FakeWavePlayer();

            bool exclusive = target.Mode == EAudioOutputMode.Exclusive && FallbackReason is null;
            string deviceId = target.FollowsWindowsDefault ? "speakers" : target.PreferredDeviceId;

            return new AudioOutputHandle(LastPlayer, deviceId, true, exclusive ? EAudioOutputMode.Exclusive : EAudioOutputMode.Shared, target.Mode == EAudioOutputMode.Exclusive ? FallbackReason : null, exclusive ? ExclusiveFormat : null);
        }
    }

    private sealed class FakeWavePlayer : IWavePlayer
    {
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public WaveFormat OutputWaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public float Volume { get; set; } = 1f;

        public bool IsDisposed { get; private set; }

        public void Init(IWaveProvider waveProvider)
        {
        }

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Pause() => PlaybackState = PlaybackState.Paused;

        public void Stop() => PlaybackState = PlaybackState.Stopped;

        public void Dispose() => IsDisposed = true;

        public void RaiseStopped(Exception? exception) => PlaybackStopped?.Invoke(this, new StoppedEventArgs(exception));
    }
}