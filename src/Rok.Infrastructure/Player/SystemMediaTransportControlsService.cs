using Microsoft.Extensions.Logging;
using Rok.Application.Dto;
using Rok.Application.Interfaces;
using Rok.Application.Messages;
using Rok.Application.Player;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

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

    public void UpdatePlaybackState(PlaybackStatus status)
    {
        if (_smtc is null)
        {
            logger.LogWarning("SMTC: UpdatePlaybackState called but _smtc is null");
            return;
        }

        try
        {
            MediaPlaybackStatus newStatus = status switch
            {
                PlaybackStatus.Playing => MediaPlaybackStatus.Playing,
                PlaybackStatus.Paused => MediaPlaybackStatus.Paused,
                PlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
                PlaybackStatus.Closed => MediaPlaybackStatus.Closed,
                _ => MediaPlaybackStatus.Stopped
            };

            logger.LogInformation("SMTC: Updating playback state from {OldStatus} to {NewStatus}", _smtc.PlaybackStatus, newStatus);

            _smtc.PlaybackStatus = newStatus;

            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC playback state");
        }
    }

    public async Task UpdateTrackInfoAsync(TrackDto track, string? coverPath)
    {
        if (_smtc is null)
            return;

        try
        {
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;

            SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = track.Title;
            updater.MusicProperties.Artist = track.ArtistName;
            updater.MusicProperties.AlbumTitle = track.AlbumName;

            if (!string.IsNullOrEmpty(coverPath))
            {
                try
                {
                    StorageFile coverFile = await StorageFile.GetFileFromPathAsync(coverPath);
                    updater.Thumbnail = RandomAccessStreamReference.CreateFromFile(coverFile);
                }
                catch (Exception coverEx)
                {
                    logger.LogDebug(coverEx, "SMTC: failed to load cover from {CoverPath}", coverPath);
                    updater.Thumbnail = null;
                }
            }
            else
            {
                updater.Thumbnail = null;
            }

            updater.Update();

            logger.LogDebug("SMTC: Updated track info — {Title} by {Artist}", track.Title, track.ArtistName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC track info");
        }
    }

    public void UpdateTimeline(TimeSpan position, TimeSpan duration)
    {
        if (_smtc is null)
            return;

        try
        {
            SystemMediaTransportControlsTimelineProperties timeline = new()
            {
                StartTime = TimeSpan.Zero,
                EndTime = duration,
                Position = position,
                MinSeekTime = TimeSpan.Zero,
                MaxSeekTime = duration
            };

            _smtc.UpdateTimelineProperties(timeline);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to update SMTC timeline");
        }
    }

    public void UpdateRadioStation(RadioStationDto station)
    {
        if (_smtc is null)
            return;

        try
        {
            SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = station.Name;
            updater.MusicProperties.Artist = station.Name;
            updater.MusicProperties.AlbumArtist = station.Name;
            updater.Thumbnail = null;
            updater.Update();

            _smtc.IsNextEnabled = false;
            _smtc.IsPreviousEnabled = false;
            _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());

            logger.LogDebug("SMTC: Updated radio station — {Station}", station.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC radio station info");
        }
    }

    public void UpdateRadioMetadata(string streamTitle)
    {
        if (_smtc is null)
            return;

        try
        {
            SystemMediaTransportControlsDisplayUpdater updater = _smtc.DisplayUpdater;
            string fallbackArtist = updater.MusicProperties.AlbumArtist ?? string.Empty;
            (string artist, string title) = ParseIcyTitle(streamTitle, fallbackArtist);
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = artist;
            updater.Update();

            logger.LogDebug("SMTC: Updated radio metadata — {Artist} / {Title}", artist, title);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update SMTC radio metadata");
        }
    }

    private static (string Artist, string Title) ParseIcyTitle(string streamTitle, string fallbackArtist)
    {
        int sep = streamTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (sep > 0)
            return (streamTitle[..sep].Trim(), streamTitle[(sep + 3)..].Trim());
        return (fallbackArtist, streamTitle.Trim());
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