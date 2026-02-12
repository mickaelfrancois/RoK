using System.Threading;
using Rok.Application.Player;
using Rok.Infrastructure.Social;

namespace Rok.Services.Player;

public class PlayerService : IPlayerService
{
    private EPlaybackState _playerState = EPlaybackState.Stopped;

    private readonly DiscordRichPresenceService? _discordService;

    private readonly IAppOptions _appOptions;

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
        private set
        {
            TrackDto? previousTrack = _currentTrack;

            _currentTrack = value;

            if ((previousTrack == null || previousTrack.Id != _currentTrack?.Id) && _currentTrack != null)
                Messenger.Send(new MediaChangedMessage(_currentTrack, previousTrack));
        }
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


    public PlayerService(IPlayerEngine player, IAppOptions appOptions, DiscordRichPresenceService? discordService, ILogger<PlayerService> logger)
    {
        _player = Guard.Against.Null(player, nameof(player));
        _appOptions = Guard.Against.Null(appOptions, nameof(appOptions));
        _discordService = discordService;
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
    }


    #region Events

    private void OnMediaStateChanged(object? sender, EventArgs e)
    {
        // Not used currently        
    }


    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media ended fired.");

        if (_isCrossfadeEnabled)
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
            _ = Task.Run(() => CrossfadeToNextTrackAsync());
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

        Start(startTrack, null);

        Messenger.Send(new PlaylistChanged(Playlist));
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

        if (itemsToInsert.Count == 0)
            return;

        if (index == null) index = _currentIndex + 1;
        if (index < 0) index = 0;
        if (index > Playlist.Count) index = Playlist.Count;

        Playlist.InsertRange(index.Value, itemsToInsert);

        Messenger.Send(new PlaylistChanged(Playlist));
    }


    public void Start(TrackDto? startTrack = null, TimeSpan? startPosition = null)
    {
        if (startTrack == null)
            _currentIndex = 0;
        else
            _currentIndex = Playlist.FindIndex(c => c.Id == startTrack.Id);

        LoadFile(Playlist[_currentIndex]);

        if (startPosition.HasValue)
            Position = startPosition.Value.TotalSeconds;

        Play();
    }

    public void Pause()
    {
        PlaybackState = EPlaybackState.Paused;

        _player.Pause();

        _discordService?.ClearPresence();
    }

    public void Play()
    {
        Volume = _volume;

        _player.Play();

        PlaybackState = EPlaybackState.Playing;

        if (CurrentTrack != null)
            UpdateDiscordPresence(CurrentTrack, isPlaying: true);
    }

    public void Stop(bool firePlaybackStateChange)
    {
        // Cancel any ongoing crossfade
        _crossfadeCts?.Cancel();
        _player.Stop();

        if (firePlaybackStateChange)
            PlaybackState = EPlaybackState.Stopped;

        _discordService?.ClearPresence();
    }

    public void Skip()
    {
        Next();
    }

    public void Next()
    {
        // Cancel any ongoing crossfade
        _crossfadeCts?.Cancel();

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

        LoadFile(Playlist[++_currentIndex]);
        Play();
    }

    public void Previous()
    {
        if (_currentIndex - 1 < 0)
        {
            if (IsLoopingEnabled)
                _currentIndex = Playlist.Count - 1;
            else
                return;
        }

        LoadFile(Playlist[--_currentIndex]);
        Play();
    }


    public void ShuffleTracks()
    {
        if (Playlist == null || Playlist.Count <= 1)
            return;

        if (_currentIndex < 0) _currentIndex = 0;
        if (_currentIndex >= Playlist.Count) _currentIndex = Playlist.Count - 1;

        int prefixCount = Math.Clamp(_currentIndex + 1, 0, Playlist.Count);

        if (prefixCount >= Playlist.Count)
            return;

        List<List<TrackDto>> groupedByArtist = Playlist
            .Skip(prefixCount)
            .GroupBy(track => track.ArtistName)
            .Select(group => group.ToList())
            .ToList();

        if (groupedByArtist.Count == 1)
        {
            List<TrackDto> singleArtistTracks = groupedByArtist[0];
            Random rng = Random.Shared;
            for (int i = singleArtistTracks.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (singleArtistTracks[j], singleArtistTracks[i]) = (singleArtistTracks[i], singleArtistTracks[j]);
            }

            Playlist.RemoveRange(prefixCount, Playlist.Count - prefixCount);
            Playlist.InsertRange(prefixCount, singleArtistTracks);
            Messenger.Send(new PlaylistChanged(Playlist));
            return;
        }

        Random random = Random.Shared;
        groupedByArtist.ForEach(group =>
        {
            for (int i = group.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (group[j], group[i]) = (group[i], group[j]);
            }
        });

        List<TrackDto> shuffledTracks = new();
        while (groupedByArtist.Any(group => group.Count > 0))
        {
            for (int i = 0; i < groupedByArtist.Count; i++)
            {
                if (groupedByArtist[i].Count > 0)
                {
                    shuffledTracks.Add(groupedByArtist[i][0]);
                    groupedByArtist[i].RemoveAt(0);
                }
            }
        }

        Playlist.RemoveRange(prefixCount, Playlist.Count - prefixCount);
        Playlist.InsertRange(prefixCount, shuffledTracks);

        Messenger.Send(new PlaylistChanged(Playlist));
    }


    #region Engine

    private void LoadFile(TrackDto track)
    {
        _player.Stop();

        bool res = _player.SetTrack(track);

        if (res)
        {
            CurrentTrack = track;

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

            int nextIndex = _currentIndex + 1;
            if (nextIndex >= Playlist.Count)
            {
                if (IsLoopingEnabled)
                    nextIndex = 0;
                else
                    return; // nothing to do
            }

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

            double masterVolume = _volume;
            const int intervalMs = 50;
            TimeSpan duration = TimeSpan.FromSeconds(crossfadeDurationSeconds);
            int steps = Math.Max(1, (int)(duration.TotalMilliseconds / intervalMs));

            await FadeOut(masterVolume, intervalMs, steps, cancellationToken);

            // Load and start next track with zero volume            
            LoadFile(nextTrack);
            _player.SetVolume(0);
            _currentIndex = nextIndex;
            _player.Play();
            PlaybackState = EPlaybackState.Playing;

            await FadeIn(masterVolume, intervalMs, steps, cancellationToken);

            UpdateDiscordPresence(nextTrack, isPlaying: true);
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
            if (_crossfadeCts != null && _crossfadeCts.IsCancellationRequested)
            {
                _crossfadeCts.Dispose();
                _crossfadeCts = null;
            }
        }
    }

    private async Task FadeIn(double masterVolume, int intervalMs, int steps, CancellationToken cancellationToken)
    {
        double lastVolume = -1;
        double minVolume = 0.05 * masterVolume;

        _logger.LogDebug("Starting fade-in over {Steps} steps with interval {Interval}ms", steps, intervalMs);

        for (int i = 0; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double progress = Math.Clamp((double)i / steps, 0.0, 1.0);
            double volume = AudioRamping.DbInterpolate(1.0 - progress, masterVolume);

            if (volume < minVolume)
                volume = minVolume;

            if (Math.Abs(volume - lastVolume) > 0.01)
            {
                _player.SetVolume(volume);
                lastVolume = volume;
            }

            await Task.Delay(intervalMs, cancellationToken);
        }

        _player.SetVolume(masterVolume);
    }


    private async Task FadeOut(double masterVolume, int intervalMs, int steps, CancellationToken cancellationToken)
    {
        double lastVolume = -1;
        double minVolume = 0.05 * masterVolume;

        _logger.LogDebug("Starting fade-out over {Steps} steps with interval {Interval}ms", steps, intervalMs);

        // Fade-out current (use dB interpolation for perceptual linearity)
        for (int i = 0; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double progress = Math.Clamp((double)i / steps, 0.0, 1.0);
            double volume = minVolume + AudioRamping.DbInterpolate(progress, masterVolume);
            if (Math.Abs(volume - lastVolume) > 0.01)
            {
                _player.SetVolume(volume);
                lastVolume = volume;
            }

            if (volume <= 0.1)
                break;

            await Task.Delay(intervalMs, cancellationToken);
        }

        _player.SetVolume(0);
    }

    #endregion
}