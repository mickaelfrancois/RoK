using Microsoft.Extensions.Logging;
using MiF.Guard;
using MiF.SimpleMessenger;
using Rok.Application.Interfaces;
using Rok.Application.Messages;
using Rok.Application.Randomizer;
using Rok.Infrastructure.Social;
using Rok.Services.Player;

namespace Rok.Application.Player;

public class PlayerService : IPlayerService
{
    private EPlaybackState _playerState = EPlaybackState.Stopped;

    private readonly IDiscordRichPresenceService? _discordService;

    private readonly ICallDetectionService _callDetectionService;

    private readonly ISystemMediaTransportControlsService? _smtcService;

    private readonly IAppOptions _appOptions;

    private readonly TimeProvider _timeProvider;

    private enum PauseReason { None, User, Call, Smtc }

    private PauseReason _pauseReason = PauseReason.None;

    private DateTime _pauseTimestampUtc = DateTime.MinValue;

    private static readonly TimeSpan KCallReconciliationWindow = TimeSpan.FromSeconds(3);

    private volatile bool _isCrossfadeRunning;

    public EPlaybackState PlaybackState
    {
        get => _playerState;
        private set
        {
            _playerState = value;

            Messenger.Send(new MediaStateChanged(_playerState));
        }
    }

    private double _volume;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            _player.SetVolume(_volume);
        }
    }

    private readonly bool _isCrossfadeEnabled;

    private CancellationTokenSource? _crossfadeCts;

    public bool IsLoopingEnabled { get; set; }

    public double Position
    {
        get => _player.Position;
        set => Task.Run(() =>
        {
            double seek = Math.Max(0, value);

            if (PlaybackState == EPlaybackState.Paused || PlaybackState == EPlaybackState.Ended)
                Play();

            _player.SetPosition(seek);
        });
    }

    public List<TrackDto> Playlist { get; private set; } = [];

    private int _currentIndex = 0;

    public bool CanSeek { get; set; } = true;

    private TrackDto? _currentTrack;

    public TrackDto? CurrentTrack
    {
        get => _currentTrack;
        private set => _currentTrack = value;
    }

    private double _volumeBeforeMute = 50;

    private const double KDefaultMuteVolume = 50;

    private bool _isMuted;

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (value)
            {
                _volumeBeforeMute = Volume;
                Volume = 0;
            }
            else
            {
                Volume = _volumeBeforeMute > 0 ? _volumeBeforeMute : KDefaultMuteVolume;
            }

            _isMuted = value;
        }
    }

    public bool CanNext
    {
        get
        {
            if (IsLoopingEnabled)
                return true;

            return _currentIndex + 1 < Playlist.Count;
        }
    }

    public bool CanPrevious
    {
        get
        {
            if (IsLoopingEnabled)
                return true;

            return _currentIndex - 1 >= 0;
        }
    }

    private readonly IPlayerEngine _player;

    private readonly ILogger<PlayerService> _logger;

    public PlayerService(ICallDetectionService callDetectionService, IPlayerEngine player, IAppOptions appOptions, IDiscordRichPresenceService? discordService, ISystemMediaTransportControlsService? smtcService, TimeProvider timeProvider, ILogger<PlayerService> logger)
    {
        _callDetectionService = Guard.Against.Null(callDetectionService, nameof(callDetectionService));
        _player = Guard.Against.Null(player, nameof(player));
        _appOptions = Guard.Against.Null(appOptions, nameof(appOptions));
        _discordService = discordService;
        _smtcService = smtcService;
        _timeProvider = Guard.Against.Null(timeProvider, nameof(timeProvider));
        _logger = Guard.Against.Null(logger, nameof(logger));

        _isCrossfadeEnabled = appOptions.CrossFade;

        _discordService?.Initialize();

        InitEvents();

#if DEBUG
        _volume = 5;
#else
        _volume = 100;
#endif
    }

    public void InitEvents()
    {
        _player.OnMediaAboutToEnd += OnMediaAboutToEnd;
        _player.OnMediaChanged += OnMediaChanged;
        _player.OnMediaEnded += OnMediaEnded;
        _player.OnMediaStateChanged += OnMediaStateChanged;

        _callDetectionService.CallStateChanged += (s, inCall) =>
        {
            if (!_appOptions.PauseOnCall)
                return;

            _logger.LogInformation("Call state changed, in call: {InCall}, current state: {State}, pause reason: {Reason}", inCall, PlaybackState, _pauseReason);

            if (inCall)
            {
                if (PlaybackState == EPlaybackState.Playing)
                {
                    Pause(PauseReason.Call);
                }
                else if (PlaybackState == EPlaybackState.Paused
                         && _pauseReason == PauseReason.Smtc
                         && _timeProvider.GetUtcNow().UtcDateTime - _pauseTimestampUtc <= KCallReconciliationWindow)
                {
                    _logger.LogInformation("Reclassifying recent SMTC pause as call-driven");
                    _pauseReason = PauseReason.Call;
                }
            }
            else
            {
                if (PlaybackState == EPlaybackState.Paused && _pauseReason == PauseReason.Call)
                {
                    Play();
                }
            }
        };
        _callDetectionService.Start();
    }

    #region Events

    public void HandleMediaControlCommand(MediaControlCommandMessage message)
    {
        _logger.LogInformation("PlayerService: received media control command {Command}", message.Command);

        switch (message.Command)
        {
            case MediaControlCommandMessage.CommandType.Play:
                Play();
                break;
            case MediaControlCommandMessage.CommandType.Pause:
                Pause(PauseReason.Smtc);
                break;
            case MediaControlCommandMessage.CommandType.Next:
                Next();
                break;
            case MediaControlCommandMessage.CommandType.Previous:
                Previous();
                break;
            case MediaControlCommandMessage.CommandType.Stop:
                Stop(true);
                break;
        }
    }

    private void OnMediaStateChanged(object? sender, EventArgs e)
    {
        // Not used currently        
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media ended fired.");

        if (_isCrossfadeEnabled && _isCrossfadeRunning)
            return;

        if (CurrentTrack != null)
            Messenger.Send(new MediaEvent(EPlaybackState.Stopped, CurrentTrack));

        Next();
    }

    private void OnMediaChanged(object? sender, EventArgs e)
    {
        // Not used currently
    }

    private void OnMediaAboutToEnd(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media about to end fired.");

        if (CurrentTrack != null)
            Messenger.Send(new MediaAboutToEndEvent(CurrentTrack));

        if (_isCrossfadeEnabled)
        {
            _isCrossfadeRunning = true;
            _ = CrossfadeToNextTrackAsync();
        }
    }

    #endregion

    public void LoadPlaylist(List<TrackDto> tracks, TrackDto? startTrack = null)
    {
        Guard.Against.Null(tracks);

        if (CurrentTrack != null)
            Messenger.Send(new MediaEvent(EPlaybackState.Ended, CurrentTrack));

        Stop(false);

        Playlist = tracks;
        _currentIndex = 0;
        _currentTrack = null;

        Start(startTrack);

        Messenger.Send(new PlaylistChanged(Playlist));
    }

    public List<TrackDto> GetQueue()
    {
        return Playlist.Skip(_currentIndex + 1).ToList();
    }

    public void AddTracksToPlaylist(List<TrackDto> tracks)
    {
        Guard.Against.Null(tracks);

        bool hasTracks = Playlist.Count > 0;

        tracks.ForEach(c => Playlist.Add(c));

        if (!hasTracks)
            Start();

        Messenger.Send(new PlaylistChanged(Playlist));
    }

    public void InsertTracksToPlaylist(List<TrackDto> tracks, int? index = null)
    {
        Guard.Against.Null(tracks);

        if (tracks.Count == 0)
            return;

        List<TrackDto> itemsToInsert = new(tracks.Count);
        itemsToInsert.AddRange(tracks);

        index ??= _currentIndex + 1;
        index = Math.Clamp(index.Value, 0, Playlist.Count);

        Playlist.InsertRange(index.Value, itemsToInsert);

        Messenger.Send(new PlaylistChanged(Playlist));
    }

    public void Start(TrackDto? startTrack = null)
    {
        if (startTrack == null)
            _currentIndex = 0;
        else
            _currentIndex = Playlist.FindIndex(c => c.Id == startTrack.Id);

        if (_currentIndex < 0)
            return;

        LoadFile(Playlist[_currentIndex]);

        Play();
    }

    public void Pause() => Pause(PauseReason.User);

    private void Pause(PauseReason reason)
    {
        PlaybackState = EPlaybackState.Paused;
        _pauseReason = reason;
        _pauseTimestampUtc = _timeProvider.GetUtcNow().UtcDateTime;

        _player.Pause();

        _discordService?.ClearPresence();
        _smtcService?.UpdatePlaybackState(false);
    }

    public void Play()
    {
        try
        {
            Volume = _volume;

            _player.Play();

            PlaybackState = EPlaybackState.Playing;
            _pauseReason = PauseReason.None;

            if (CurrentTrack != null)
            {
                UpdateDiscordPresence(CurrentTrack, isPlaying: true);
                _smtcService?.UpdateTrackInfo(CurrentTrack);
                _smtcService?.UpdatePlaybackState(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume playback, audio device may be unavailable");
            PlaybackState = EPlaybackState.Stopped;
        }
    }

    public void Stop(bool firePlaybackStateChange)
    {
        _crossfadeCts?.Cancel();
        _player.Stop();

        if (firePlaybackStateChange)
            PlaybackState = EPlaybackState.Stopped;

        _pauseReason = PauseReason.None;

        _discordService?.ClearPresence();
        _smtcService?.UpdatePlaybackState(false);
    }

    public void Skip()
    {
        Next();
    }

    public void Next()
    {
        // Cancel any ongoing crossfade
        _crossfadeCts?.Cancel();
        _isCrossfadeRunning = false;

        if (_currentIndex + 1 >= Playlist.Count)
        {
            if (IsLoopingEnabled)
            {
                _currentIndex = 0;
            }
            else
            {
                // Playlist ended
                PlaybackState = EPlaybackState.Stopped;
                return;
            }
        }
        else
            _currentIndex++;

        LoadFile(Playlist[_currentIndex]);
        Play();
    }

    public void Previous()
    {
        if (_currentIndex - 1 < 0)
        {
            if (IsLoopingEnabled)
                _currentIndex = Playlist.Count - 1;
            else
            {
                PlaybackState = EPlaybackState.Stopped;
                return;
            }
        }
        else
            _currentIndex--;

        LoadFile(Playlist[_currentIndex]);
        Play();
    }

    public void ShuffleTracks()
    {
        TracksRandomizer.ArtistBalancedTrackRandomize(Playlist, _currentIndex);

        Messenger.Send(new PlaylistChanged(Playlist));
    }

    #region Engine

    private void LoadFile(TrackDto track)
    {
        long durationPlayed = (long)_player.Position;
        _player.Stop();

        bool res = _player.SetTrack(track);

        if (res)
        {
            TrackDto? previousTrack = CurrentTrack;
            CurrentTrack = track;

            if ((previousTrack == null || previousTrack.Id != _currentTrack?.Id) && _currentTrack != null)
                Messenger.Send(new MediaChangedMessage(_currentTrack, previousTrack, durationPlayed));

            UpdateDiscordPresence(track, isPlaying: false);
        }
    }

    private void UpdateDiscordPresence(TrackDto track, bool isPlaying)
    {
        if (_discordService == null || !_appOptions.DiscordRichPresenceEnabled)
            return;

        try
        {
            if (isPlaying)
            {
                _discordService.UpdatePresence(
                    trackTitle: track.Title,
                    artistName: track.ArtistName,
                    albumName: track.AlbumName,
                    elapsed: TimeSpan.FromSeconds(_player.Position),
                    duration: TimeSpan.FromMilliseconds(track.Duration)
                );
            }
            else
            {
                _discordService.ClearPresence();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour Discord Presence");
        }
    }

    private async Task CrossfadeToNextTrackAsync()
    {
        try
        {
            if (_crossfadeCts != null)
            {
                try
                {
                    await _crossfadeCts.CancelAsync();
                }
                catch { /* ignore */ }
                finally
                {
                    _crossfadeCts.Dispose();
                    _crossfadeCts = null;
                }
            }

            _crossfadeCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _crossfadeCts.Token;

            if (!TryGetNextIndex(out int nextIndex))
                return;

            TrackDto nextTrack = Playlist[nextIndex];
            double trackLength = _player.Length;
            double currentPosition = _player.Position;

            if (_isMuted || CurrentTrack == null || (CurrentTrack.IsAlbumLive && nextTrack.IsAlbumLive))
            {
                _logger.LogDebug("No crossfade between two live albums");

                double timeToWait = Math.Max(0, trackLength - currentPosition);

                if (timeToWait > 0)
                    await Task.Delay(TimeSpan.FromSeconds(timeToWait), cancellationToken);

                Next();
                return;
            }

            await RunCrossfadeAsync(nextIndex, nextTrack, trackLength, currentPosition, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Crossfade canceled.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during crossfade.");
        }
        finally
        {
            _isCrossfadeRunning = false;

            if (_crossfadeCts != null && _crossfadeCts.IsCancellationRequested)
            {
                _crossfadeCts.Dispose();
                _crossfadeCts = null;
            }
        }
    }

    private bool TryGetNextIndex(out int nextIndex)
    {
        nextIndex = _currentIndex + 1;

        if (nextIndex < Playlist.Count)
            return true;

        if (!IsLoopingEnabled)
            return false;

        nextIndex = 0;
        return true;
    }

    private async Task RunCrossfadeAsync(int nextIndex, TrackDto nextTrack, double trackLength, double currentPosition, CancellationToken cancellationToken)
    {
        double remainingTime = Math.Max(0, trackLength - currentPosition);
        double crossfadeDurationSeconds = Math.Min(_player.CrossfadeDelay, remainingTime);

        if (crossfadeDurationSeconds <= 0)
        {
            _logger.LogDebug("No crossfade possible, remaining time: {RemainingTime}s", remainingTime);

            if (remainingTime > 0)
                await Task.Delay(TimeSpan.FromSeconds(remainingTime), cancellationToken);

            Next();
            return;
        }

        double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - currentPosition - crossfadeDurationSeconds);

        if (timeToWaitBeforeCrossfade > 0)
            await Task.Delay(TimeSpan.FromSeconds(timeToWaitBeforeCrossfade), cancellationToken);

        _logger.LogDebug("Starting simultaneous crossfade to {Track} over {Duration}s", nextTrack.Title, crossfadeDurationSeconds);

        TrackDto? previousTrack = CurrentTrack;
        long durationPlayed = (long)_player.Position;

        await _player.CrossfadeToAsync(nextTrack, crossfadeDurationSeconds, _volume, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        _currentIndex = nextIndex;
        CurrentTrack = nextTrack;

        if (previousTrack == null || previousTrack.Id != nextTrack.Id)
            Messenger.Send(new MediaChangedMessage(nextTrack, previousTrack, durationPlayed));

        PlaybackState = EPlaybackState.Playing;
        UpdateDiscordPresence(nextTrack, isPlaying: true);
        _smtcService?.UpdateTrackInfo(nextTrack);
        _smtcService?.UpdatePlaybackState(true);
    }

    #endregion
}