using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Messages;
using Rok.Application.Player;
using Rok.Application.Player.Output;

namespace Rok.ApplicationTests.Player.Output;

public class AudioOutputCoordinatorTests
{
    private const string Speakers = "speakers";
    private const string Dac = "dac";

    private readonly Mock<IPlayerEngine> _engine = new();
    private readonly Mock<IPlayerService> _playerService = new();
    private readonly Mock<IAudioDeviceService> _deviceService = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly Messenger _messenger = new();

    public AudioOutputCoordinatorTests()
    {
        _options.SetupGet(o => o.OutputDeviceId).Returns(Dac);
        _options.SetupGet(o => o.OutputMode).Returns(EAudioOutputMode.Shared);
        _playerService.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Playing);
        _engine.SetupGet(e => e.OutputState).Returns(AudioOutputState.Closed);
    }

    private AudioOutputCoordinator BuildCoordinator() =>
        new(_engine.Object, _playerService.Object, _deviceService.Object, _options.Object, _messenger, NullLogger<AudioOutputCoordinator>.Instance);

    private static AudioDeviceSnapshot Snapshot(string? defaultId, params string[] active) =>
        new([.. active.Select(id => new AudioDeviceDto(id, id))], defaultId);

    private void GivenDevices(AudioDeviceSnapshot snapshot) => _deviceService.Setup(d => d.GetSnapshot()).Returns(snapshot);

    private void RaiseOutputLost(bool wasOnPreferred = true) =>
        _engine.Raise(e => e.OnOutputLost += null, _engine.Object, new OutputLostEventArgs(true, wasOnPreferred));

    private void RaiseDevicesChanged(AudioDeviceSnapshot snapshot) =>
        _deviceService.Raise(d => d.DevicesChanged += null, _deviceService.Object, snapshot);

    [Fact(DisplayName = "coordinator_pushes_target_from_options_at_startup")]
    public void Start_PushesTargetFromOptions()
    {
        // Arrange
        _options.SetupGet(o => o.OutputMode).Returns(EAudioOutputMode.Exclusive);
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        sut.Start();

        // Assert
        _engine.Verify(e => e.SetOutputTarget(new AudioOutputTarget(Dac, EAudioOutputMode.Exclusive)), Times.Once);
    }

    [Fact(DisplayName = "coordinator_pauses_and_never_reopens_when_preferred_is_unplugged")]
    public void OutputLost_Pauses_WhenPreferredIsUnplugged()
    {
        // Arrange
        GivenDevices(Snapshot(Speakers, Speakers));
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        RaiseOutputLost();

        // Assert
        _playerService.Verify(p => p.Pause(), Times.Once);
        _engine.Verify(e => e.ReopenOutput(), Times.Never);
    }

    [Fact(DisplayName = "coordinator_does_not_pause_when_already_paused")]
    public void OutputLost_DoesNotPause_WhenAlreadyPaused()
    {
        // Arrange
        GivenDevices(Snapshot(Speakers, Speakers));
        _playerService.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Paused);
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        RaiseOutputLost();

        // Assert
        _playerService.Verify(p => p.Pause(), Times.Never);
        _engine.Verify(e => e.ReopenOutput(), Times.Never);
    }

    [Fact(DisplayName = "coordinator_reopens_on_replug_without_resuming_when_paused")]
    public void DevicesChanged_Reopens_WhenPreferredComesBack()
    {
        // Arrange
        _playerService.SetupGet(p => p.PlaybackState).Returns(EPlaybackState.Paused);
        _engine.SetupGet(e => e.OutputState).Returns(new AudioOutputState(true, Speakers, false, EAudioOutputMode.Shared, null, true));
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        RaiseDevicesChanged(Snapshot(Speakers, Speakers, Dac));

        // Assert
        _engine.Verify(e => e.ReopenOutput(), Times.Once);
        _playerService.Verify(p => p.Play(), Times.Never);
    }

    [Fact(DisplayName = "coordinator_pauses_when_devices_change_removes_the_preferred_first")]
    public void DevicesChanged_Pauses_WhenPreferredDisappearsBeforeOutputFails()
    {
        // Arrange
        _engine.SetupGet(e => e.OutputState).Returns(new AudioOutputState(true, Dac, true, EAudioOutputMode.Shared, null, true));
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        RaiseDevicesChanged(Snapshot(Speakers, Speakers));

        // Assert
        _playerService.Verify(p => p.Pause(), Times.Once);
        _engine.Verify(e => e.ReopenOutput(), Times.Never);
    }

    [Fact(DisplayName = "coordinator_follows_windows_default_without_pause")]
    public void DevicesChanged_FollowsWindowsDefault_WithoutPause()
    {
        // Arrange
        _options.SetupGet(o => o.OutputDeviceId).Returns(string.Empty);
        _engine.SetupGet(e => e.OutputState).Returns(new AudioOutputState(true, Speakers, true, EAudioOutputMode.Shared, null, true));
        GivenDevices(Snapshot(Dac, Speakers, Dac));
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        RaiseDevicesChanged(Snapshot(Dac, Speakers, Dac));
        RaiseOutputLost();

        // Assert
        _engine.Verify(e => e.ReopenOutput(), Times.Exactly(2));
        _playerService.Verify(p => p.Pause(), Times.Never);
    }

    [Fact(DisplayName = "coordinator_applies_new_target_on_output_options_changed")]
    public void OutputOptionsChanged_AppliesNewTarget()
    {
        // Arrange
        using AudioOutputCoordinator sut = BuildCoordinator();
        _options.SetupGet(o => o.OutputDeviceId).Returns(Speakers);
        _options.SetupGet(o => o.OutputMode).Returns(EAudioOutputMode.Exclusive);

        // Act
        _messenger.Send(new AudioOutputOptionsChanged());

        // Assert
        _engine.Verify(e => e.SetOutputTarget(new AudioOutputTarget(Speakers, EAudioOutputMode.Exclusive)), Times.Once);
    }

    [Fact(DisplayName = "coordinator_forwards_engine_state_as_message")]
    public void OutputStateChanged_IsForwardedAsMessage()
    {
        // Arrange
        AudioOutputState state = new(true, Dac, true, EAudioOutputMode.Exclusive, null, true);
        AudioOutputStateChanged? received = null;
        using IDisposable subscription = _messenger.Subscribe<AudioOutputStateChanged>(message => received = message);
        using AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        _engine.Raise(e => e.OnOutputStateChanged += null, _engine.Object, state);

        // Assert
        Assert.NotNull(received);
        Assert.Equal(state, received.State);
        Assert.False(received.IsLive);
    }

    [Fact(DisplayName = "coordinator_dispose_releases_every_subscription")]
    public void Dispose_ReleasesEverySubscription()
    {
        // Arrange
        GivenDevices(Snapshot(Speakers, Speakers));
        _engine.SetupGet(e => e.OutputState).Returns(new AudioOutputState(true, Speakers, false, EAudioOutputMode.Shared, null, true));
        AudioOutputCoordinator sut = BuildCoordinator();

        // Act
        sut.Dispose();
        RaiseOutputLost();
        RaiseDevicesChanged(Snapshot(Speakers, Speakers, Dac));
        _messenger.Send(new AudioOutputOptionsChanged());

        // Assert
        _playerService.Verify(p => p.Pause(), Times.Never);
        _engine.Verify(e => e.ReopenOutput(), Times.Never);
        _engine.Verify(e => e.SetOutputTarget(It.IsAny<AudioOutputTarget>()), Times.Never);
    }
}