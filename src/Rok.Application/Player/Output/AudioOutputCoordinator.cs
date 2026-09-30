using CleanArch.DevKit.Guards;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Messages;

namespace Rok.Application.Player.Output;

/// <summary>
/// Applies the output decisions of <see cref="AudioDevicePolicy"/>: pushes the requested target to the engine,
/// pauses or reopens when a device disappears or comes back, and forwards the output state as a message.
/// </summary>
public sealed class AudioOutputCoordinator : IDisposable
{
    private readonly IPlayerEngine _engine;
    private readonly IPlayerService _playerService;
    private readonly IAudioDeviceService _deviceService;
    private readonly IAppOptions _options;
    private readonly IMessenger _messenger;
    private readonly ILogger<AudioOutputCoordinator> _logger;
    private readonly IDisposable _optionsSubscription;
    private bool _disposed;

    public AudioOutputCoordinator(IPlayerEngine engine, IPlayerService playerService, IAudioDeviceService deviceService, IAppOptions options, IMessenger messenger, ILogger<AudioOutputCoordinator> logger)
    {
        _engine = Guard.NotNull(engine);
        _playerService = Guard.NotNull(playerService);
        _deviceService = Guard.NotNull(deviceService);
        _options = Guard.NotNull(options);
        _messenger = Guard.NotNull(messenger);
        _logger = Guard.NotNull(logger);

        _engine.OnOutputLost += Engine_OnOutputLost;
        _engine.OnOutputStateChanged += Engine_OnOutputStateChanged;
        _deviceService.DevicesChanged += DeviceService_DevicesChanged;
        _optionsSubscription = _messenger.Subscribe<AudioOutputOptionsChanged>(_ => ApplyTarget());
    }

    /// <summary>Pushes the target saved in the options to the engine; called once at startup.</summary>
    public void Start() => ApplyTarget();

    private AudioOutputTarget CurrentTarget => new(_options.OutputDeviceId ?? string.Empty, _options.OutputMode);

    private void ApplyTarget()
    {
        AudioOutputTarget target = CurrentTarget;

        _logger.LogInformation("Audio output target set to {Device} in {Mode} mode", target.FollowsWindowsDefault ? "the Windows default device" : target.PreferredDeviceId, target.Mode);
        _engine.SetOutputTarget(target);
    }

    private void Engine_OnOutputLost(object? sender, OutputLostEventArgs e)
    {
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnOutputLost(CurrentTarget, e, _deviceService.GetSnapshot());

        _logger.LogWarning("Audio output lost (playing: {WasPlaying}, on chosen device: {WasOnPreferred}), action: {Action}", e.WasPlaying, e.WasOnPreferred, action);

        Apply(action);
    }

    private void DeviceService_DevicesChanged(object? sender, AudioDeviceSnapshot snapshot)
    {
        EAudioDeviceAction action = AudioDevicePolicy.DecideOnDevicesChanged(CurrentTarget, _engine.OutputState, snapshot);

        if (action != EAudioDeviceAction.None)
            _logger.LogInformation("Audio devices changed, action: {Action}", action);

        Apply(action);
    }

    private void Engine_OnOutputStateChanged(object? sender, AudioOutputState state) =>
        _messenger.Send(new AudioOutputStateChanged(state, _engine.IsLive));

    private void Apply(EAudioDeviceAction action)
    {
        switch (action)
        {
            case EAudioDeviceAction.Pause:
                if (_playerService.PlaybackState == EPlaybackState.Playing)
                    _playerService.Pause();
                break;

            case EAudioDeviceAction.Reopen:
                _engine.ReopenOutput();
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _engine.OnOutputLost -= Engine_OnOutputLost;
        _engine.OnOutputStateChanged -= Engine_OnOutputStateChanged;
        _deviceService.DevicesChanged -= DeviceService_DevicesChanged;
        _optionsSubscription.Dispose();
    }
}