using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Messages;
using Rok.Application.Player;
using Windows.Media;
using Windows.Media.Playback;

namespace Rok.Infrastructure.Player;

/// <summary>
/// Integrates with Windows System Media Transport Controls to handle Bluetooth AVRCP commands.
/// Commands are dispatched via direct call to <see cref="IPlayerService.HandleMediaControlCommand"/>
/// to avoid circular dependency (set up in App.xaml.cs after DI container is built).
/// </summary>
public sealed class SystemMediaTransportControlsService(ILogger<SystemMediaTransportControlsService> logger) : ISystemMediaTransportControlsService, IDisposable
{
    private MediaPlayer? _mediaPlayer;
    private SystemMediaTransportControls? _smtc;
    private bool _isInitialized;
    private IPlayerService? _playerService;

    public void SetPlayerService(IPlayerService playerService)
    {
        _playerService = playerService;
        logger.LogInformation("SMTC: PlayerService set");
    }

    public void Initialize()
    {
        if (_isInitialized)
        {
            logger.LogWarning("SMTC: already initialized");
            return;
        }

        try
        {

            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;

            _smtc = _mediaPlayer.SystemMediaTransportControls;
            _smtc.ButtonPressed += OnButtonPressed;

            _smtc.IsEnabled = true;
            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;

            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;

            _isInitialized = true;

            logger.LogInformation("SMTC: initialized with PlaybackStatus = Stopped, CommandManager disabled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTC: initialization FAILED");
        }
    }

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        logger.LogInformation("SMTC button pressed: {Button}", args.Button);

        if (_playerService is null)
        {
            logger.LogWarning("SMTC: PlayerService not set, ignoring button press");
            return;
        }

        try
        {
            MediaControlCommandMessage.CommandType? command = args.Button switch
            {
                SystemMediaTransportControlsButton.Play => MediaControlCommandMessage.CommandType.Play,
                SystemMediaTransportControlsButton.Pause => MediaControlCommandMessage.CommandType.Pause,
                SystemMediaTransportControlsButton.Next => MediaControlCommandMessage.CommandType.Next,
                SystemMediaTransportControlsButton.Previous => MediaControlCommandMessage.CommandType.Previous,
                SystemMediaTransportControlsButton.Stop => MediaControlCommandMessage.CommandType.Stop,
                _ => null
            };

            if (command.HasValue)
            {
                logger.LogInformation("SMTC: dispatching {Command} command", command.Value);
                _playerService.HandleMediaControlCommand(new MediaControlCommandMessage(command.Value));
            }
            else
            {
                logger.LogWarning("SMTC: unmapped button {Button}", args.Button);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling SMTC button: {Button}", args.Button);
        }
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        if (_smtc is null)
        {
            logger.LogWarning("SMTC: UpdatePlaybackState called but _smtc is null");
            return;
        }

        try
        {
            MediaPlaybackStatus newStatus = isPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;

            logger.LogInformation("SMTC: Updating playback state from {OldStatus} to {NewStatus}", _smtc.PlaybackStatus, newStatus);

            _smtc.PlaybackStatus = newStatus;

            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;

            logger.LogDebug("SMTC: Play and Pause buttons re-enabled");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC playback state");
        }
    }

    public void UpdateTrackInfo(TrackDto track)
    {
        if (_smtc is null)
            return;

        try
        {
            SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = track.Title;
            updater.MusicProperties.Artist = track.ArtistName;
            updater.MusicProperties.AlbumTitle = track.AlbumName;
            updater.Update();

            logger.LogDebug("SMTC: Updated track info — {Title} by {Artist}", track.Title, track.ArtistName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC track info");
        }
    }

    public void Dispose()
    {
        logger.LogInformation("SMTC: disposing");

        if (_smtc is not null)
        {
            _smtc.ButtonPressed -= OnButtonPressed;
            _smtc.IsEnabled = false;
            _smtc = null;
        }

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
    }
}
