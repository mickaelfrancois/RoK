using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CleanArch.DevKit.Guards;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Messages;
using Rok.Application.Player.Mix;
using Rok.Application.Randomizer;
using Rok.Services.Player;

namespace Rok.Application.Player;

public sealed class PlayerService : IPlayerService, IDisposable
{
    private EPlaybackState _playerState = EPlaybackState.Stopped;

    private EPlaybackMode _mode = EPlaybackMode.None;

    private RadioStationDto? _currentStation;

    private string? _currentStreamTitle;

    private readonly IDiscordRichPresenceService? _discordService;

    private readonly ICallDetectionService _callDetectionService;

    private readonly ISystemMediaTransportControlsService? _smtcService;

    private readonly IAlbumPicture _albumPicture;

    private readonly IAppOptions _appOptions;

    private readonly TimeProvider _timeProvider;

    private enum PauseReason { None, User, Call, Smtc }

    private PauseReason _pauseReason = PauseReason.None;

    private DateTime _pauseTimestampUtc = DateTime.MinValue;

    private static readonly TimeSpan KCallReconciliationWindow = TimeSpan.FromSeconds(3);

    private volatile bool _isCrossfadeRunning;
    private bool _outgoingEndedDuringCrossfade;

    private bool _lastIsBuffering;

    private ITimer? _smtcTimelineTimer;

    public EPlaybackState PlaybackState
    {
        get => _playerState;
        private set
        {
            _playerState = value;

            _messenger.Send(new MediaStateChanged(_playerState));
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


    private CancellationTokenSource? _crossfadeCts;

    private readonly Lock _transitionLock = new();

    private TrackDto? _pendingGapless;

    private TrackDto? _invalidatedGapless;

    private bool _isLoopingEnabled;

    private MixPlan? _mixPlan;

    private CancellationTokenSource? _mixCts;

    private long _mixGeneration;

    private long _crossfadeGeneration;

    public bool IsLoopingEnabled
    {
        get => _isLoopingEnabled;
        set
        {
            if (_isLoopingEnabled == value)
                return;

            _isLoopingEnabled = value;

            if (CurrentTrack != null)
                RequestMixPreparation();
        }
    }

    public double Position
    {
        get => _player.Position;
        set
        {
            if (_mode == EPlaybackMode.Radio)
                return;

            _ = Task.Run(() =>
            {
                double seek = Math.Max(0, value);

                InvalidatePendingTransition();

                if (PlaybackState == EPlaybackState.Paused || PlaybackState == EPlaybackState.Ended)
                    Play();

                _player.SetPosition(seek);
            });
        }
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
            if (_mode == EPlaybackMode.Radio)
                return false;

            if (IsLoopingEnabled)
                return true;

            return _currentIndex + 1 < Playlist.Count;
        }
    }

    public bool CanPrevious
    {
        get
        {
            if (_mode == EPlaybackMode.Radio)
                return false;

            if (IsLoopingEnabled)
                return true;

            return _currentIndex - 1 >= 0;
        }
    }

    public EPlaybackMode Mode => _mode;

    public RadioStationDto? CurrentStation => _currentStation;

    public string? CurrentStreamTitle => _currentStreamTitle;

    public bool IsBuffering => _player.IsBuffering;

    private readonly IPlayerEngine _player;

    private readonly ILogger<PlayerService> _logger;

    private readonly IMessenger _messenger;

    private readonly IMixCueProvider _mixCues;

    private readonly IDisposable _replayGainSubscription;

    public PlayerService(ICallDetectionService callDetectionService, IPlayerEngine player, IAppOptions appOptions, IDiscordRichPresenceService? discordService, ISystemMediaTransportControlsService? smtcService, IAlbumPicture albumPicture, TimeProvider timeProvider, IMessenger messenger, IMixCueProvider mixCues, ILogger<PlayerService> logger)
    {
        _callDetectionService = Guard.NotNull(callDetectionService, nameof(callDetectionService));
        _player = Guard.NotNull(player, nameof(player));
        _appOptions = Guard.NotNull(appOptions, nameof(appOptions));
        _discordService = discordService;
        _smtcService = smtcService;
        _albumPicture = Guard.NotNull(albumPicture, nameof(albumPicture));
        _timeProvider = Guard.NotNull(timeProvider, nameof(timeProvider));
        _messenger = Guard.NotNull(messenger, nameof(messenger));
        _mixCues = Guard.NotNull(mixCues, nameof(mixCues));
        _logger = Guard.NotNull(logger, nameof(logger));


        _discordService?.Initialize();

        InitEvents();

        _replayGainSubscription = _messenger.Subscribe<ReplayGainOptionsChanged>(_ => ApplyReplayGainOptions());

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
        _player.OnGaplessTransition += OnGaplessTransition;
        _player.OnTransitionCue += OnTransitionCue;
        _player.OnMetadataChanged += (_, title) =>
        {
            _currentStreamTitle = title;
            _messenger.Send(new RadioMetadataChanged(title));
            _smtcService?.UpdateRadioMetadata(title);
            _discordService?.UpdateRadioMetadata(title);
        };

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
        if (_mode == EPlaybackMode.Radio && _player.IsBuffering != _lastIsBuffering)
        {
            _lastIsBuffering = _player.IsBuffering;
            _messenger.Send(new BufferingChanged(_lastIsBuffering));
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media ended fired.");

        lock (_transitionLock)
        {
            if (_isCrossfadeRunning)
            {
                _outgoingEndedDuringCrossfade = true;

                return;
            }
        }

        if (CurrentTrack != null)
            _messenger.Send(new MediaEvent(EPlaybackState.Stopped, CurrentTrack));

        Next();
    }

    private void OnMediaChanged(object? sender, EventArgs e)
    {
        // Not used currently
    }

    private void OnMediaAboutToEnd(object? sender, EventArgs e)
    {
        _logger.LogDebug("Event Media about to end fired.");

        if (CurrentTrack == null)
            return;

        _messenger.Send(new MediaAboutToEndEvent(CurrentTrack));

        if (!TryPeekNext(out int nextIndex, out TrackDto? nextTrack))
            return;

        bool crossfadeAllowed = PlaybackTransitionPolicy.IsCrossfadeAllowed(_appOptions.CrossFade, _appOptions.OutputMode);

        if (PlaybackTransitionPolicy.Decide(crossfadeAllowed, _isMuted, CurrentTrack, nextTrack) == EPlaybackTransition.Gapless)
        {
            QueueGaplessTransition(nextIndex, nextTrack);
            return;
        }

        if (_isCrossfadeRunning)
            return;

        if (IsMixActive() && PlaybackTransitionPolicy.IsMixAllowed(CurrentTrack, nextTrack))
        {
            MixPlan? plan;

            lock (_transitionLock)
            {
                plan = _mixPlan;
            }

            if (plan != null && plan.OutgoingTrackId == CurrentTrack.Id && plan.IncomingTrackId == nextTrack.Id && plan.StartSeconds > _player.Position)
                return;

            _logger.LogInformation("Mix: no usable plan for {Track}, classic crossfade", CurrentTrack.Title);
        }

        if (!TryBeginCrossfade())
            return;

        _ = CrossfadeToNextTrackAsync(null);
    }

    private bool TryBeginCrossfade()
    {
        lock (_transitionLock)
        {
            if (_isCrossfadeRunning)
                return false;

            _isCrossfadeRunning = true;
            _outgoingEndedDuringCrossfade = false;

            return true;
        }
    }

    private bool IsMixActive() => _appOptions.MixMode && PlaybackTransitionPolicy.IsCrossfadeAllowed(_appOptions.CrossFade, _appOptions.OutputMode);

    private void OnTransitionCue(object? sender, EventArgs e)
    {
        try
        {
            TrackDto? current = CurrentTrack;

            if (current == null || _mode != EPlaybackMode.Music || !IsMixActive() || _isMuted)
                return;

            TrackDto? next = PeekNext();

            if (next == null || PlaybackTransitionPolicy.Decide(true, _isMuted, current, next) != EPlaybackTransition.Crossfade || !PlaybackTransitionPolicy.IsMixAllowed(current, next))
                return;

            MixPlan? plan;

            lock (_transitionLock)
            {
                plan = _mixPlan;
            }

            if (plan == null || plan.OutgoingTrackId != current.Id || plan.IncomingTrackId != next.Id)
            {
                _logger.LogDebug("Ignoring stale transition cue for {Track}", current.Title);
                return;
            }

            if (!TryBeginCrossfade())
                return;

            _ = CrossfadeToNextTrackAsync(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling the transition cue.");
        }
    }

    private void CancelMixPreparation(out bool hadState)
    {
        CancellationTokenSource? cts;

        lock (_transitionLock)
        {
            cts = _mixCts;
            hadState = cts != null || _mixPlan != null;
            _mixCts = null;
            _mixPlan = null;
            _mixGeneration++;
        }

        if (cts == null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        cts.Dispose();
    }

    private void RequestMixPreparation()
    {
        CancelMixPreparation(out bool hadState);

        if (!IsMixActive())
        {
            if (hadState)
                _player.ClearTransitionCue();

            return;
        }

        _player.ClearTransitionCue();

        TrackDto? current = CurrentTrack;

        if (current == null || _mode != EPlaybackMode.Music)
            return;

        TrackDto? next = PeekNext();

        if (next == null || PlaybackTransitionPolicy.Decide(true, _isMuted, current, next) != EPlaybackTransition.Crossfade)
            return;

        if (!PlaybackTransitionPolicy.IsMixAllowed(current, next))
        {
            _logger.LogInformation("Mix: skipped, live track ({Track} -> {Next})", current.Title, next.Title);

            return;
        }

        CancellationTokenSource cts = new();
        long generation;

        lock (_transitionLock)
        {
            _mixCts = cts;
            generation = _mixGeneration;
        }

        _ = ArmMixAsync(current, next, generation, cts.Token);
    }

    private async Task ArmMixAsync(TrackDto current, TrackDto next, long generation, CancellationToken ct)
    {
        try
        {
            OutroCues? outro = await _mixCues.GetOutroAsync(current, ct).ConfigureAwait(false);
            IntroCues? intro = await _mixCues.GetIntroAsync(next, ct).ConfigureAwait(false);

            if (ct.IsCancellationRequested)
                return;

            double trackLength = _player.Length > 0 ? _player.Length : current.Duration;
            MixPlan? plan = outro == null || intro == null
                ? null
                : MixTransitionPlanner.Plan(current, outro, next, intro, trackLength, _appOptions.CrossfadeDurationSeconds);

            if (plan == null)
            {
                _logger.LogInformation("Mix: no cues for {Track}, classic crossfade", current.Title);
                return;
            }

            _logger.LogInformation(
                "Mix: {Track} -> {Next}, beat-aligned {Aligned}, BPM {OutgoingBpm}/{IncomingBpm}, start shift {Shift}s, bass swap at {SwapAt}s, mix point {MixPoint}, score {MixPointScore}, stretch {Stretch}, bars {Bars}",
                current.Title,
                next.Title,
                plan.Alignment != null,
                outro?.Beats?.Bpm,
                intro?.Beats?.Bpm,
                plan.Alignment?.StartShiftSeconds ?? 0,
                plan.BassSwapAtSeconds,
                plan.MixPointScore != null,
                outro?.MixPoint?.Score,
                plan.Stretch?.Ratio.ToString("0.000", CultureInfo.InvariantCulture) ?? "none",
                plan.Stretch?.Bars ?? 0);

            lock (_transitionLock)
            {
                if (ct.IsCancellationRequested || generation != _mixGeneration || CurrentTrack?.Id != current.Id || PeekNextLocked()?.Id != next.Id)
                    return;

                _mixPlan = plan;
                _player.SetTransitionCue(current.Id, plan.StartSeconds);
            }
        }
        catch (OperationCanceledException)
        {
            // Preparation cancelled.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mix: preparation failed for {Track}, classic crossfade", current.Title);
        }
    }

    private void QueueGaplessTransition(int nextIndex, TrackDto nextTrack)
    {
        lock (_transitionLock)
        {
            _pendingGapless = nextTrack;
            _invalidatedGapless = null;
        }

        if (_player.QueueNextTrack(nextTrack, ResolveReplayGain(nextIndex)))
            return;

        _logger.LogDebug("Gapless refused for {Track}, the next track will be reloaded at the end", nextTrack.Title);

        lock (_transitionLock)
        {
            _pendingGapless = null;
        }
    }

    private void OnGaplessTransition(object? sender, GaplessTransitionEventArgs e)
    {
        _ = HandleGaplessTransitionAsync(e);
    }

    private async Task HandleGaplessTransitionAsync(GaplessTransitionEventArgs e)
    {
        try
        {
            TrackDto? pending;
            TrackDto? invalidated;

            lock (_transitionLock)
            {
                pending = _pendingGapless;
                invalidated = _invalidatedGapless;
                _pendingGapless = null;
                _invalidatedGapless = null;
            }

            if (pending?.Id == e.Track.Id && TryPeekNext(out int nextIndex, out TrackDto? queuedNext) && queuedNext.Id == e.Track.Id)
            {
                if (CurrentTrack != null)
                    _messenger.Send(new MediaEvent(EPlaybackState.Stopped, CurrentTrack));

                await AdvanceToWithoutLoadAsync(nextIndex, e.Track, (long)e.PreviousTrackPosition);
                return;
            }

            if (pending?.Id == e.Track.Id || invalidated?.Id == e.Track.Id)
            {
                _logger.LogInformation("Gapless switch to {Track} no longer matches the queue, reloading the next track", e.Track.Title);
                Next();
                return;
            }

            _logger.LogDebug("Ignoring stale gapless switch to {Track}", e.Track.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during gapless transition.");
        }
    }

    private TrackDto? PeekNext()
    {
        lock (_transitionLock)
        {
            return PeekNextLocked();
        }
    }

    private TrackDto? PeekNextLocked() => TryGetNextIndexLocked(out int nextIndex) ? Playlist[nextIndex] : null;

    private bool TryPeekNext(out int nextIndex, [NotNullWhen(true)] out TrackDto? nextTrack)
    {
        lock (_transitionLock)
        {
            if (TryGetNextIndexLocked(out nextIndex))
            {
                nextTrack = Playlist[nextIndex];

                return true;
            }
        }

        nextTrack = null;

        return false;
    }

    private void InvalidatePendingTransition()
    {
        CancelCrossfade();
        _player.ClearNextTrack();

        lock (_transitionLock)
        {
            _invalidatedGapless = _pendingGapless ?? _invalidatedGapless;
            _pendingGapless = null;
        }
    }

    private void InvalidateIfImminentChanged(TrackDto? imminentBefore)
    {
        if (PeekNext()?.Id == imminentBefore?.Id)
            return;

        InvalidatePendingTransition();
        RequestMixPreparation();
    }

    private void ResetPendingTransition()
    {
        lock (_transitionLock)
        {
            _pendingGapless = null;
            _invalidatedGapless = null;
        }
    }

    #endregion

    public void LoadPlaylist(List<TrackDto> tracks, TrackDto? startTrack = null)
    {
        Guard.NotNull(tracks);

        StopForModeSwitch();

        if (CurrentTrack != null)
            _messenger.Send(new MediaEvent(EPlaybackState.Ended, CurrentTrack));

        Stop(false);

        _mode = EPlaybackMode.Music;

        lock (_transitionLock)
        {
            Playlist = tracks;
            _currentIndex = 0;
        }

        _currentTrack = null;

        Start(startTrack);

        _messenger.Send(new PlaylistChanged(Playlist));
    }

    public List<TrackDto> GetQueue()
    {
        return Playlist.Skip(_currentIndex + 1).ToList();
    }

    public int CountUpcomingByTrack(long trackId) => CountUpcoming(track => track.Id == trackId);

    public int CountUpcomingByAlbum(long albumId) => CountUpcoming(track => track.AlbumId == albumId);

    public int CountUpcomingByArtist(long artistId) => CountUpcoming(track => track.ArtistId == artistId);

    public int CountUpcomingByGenre(long genreId) => CountUpcoming(track => track.GenreId == genreId);

    public int RemoveUpcomingByTrack(long trackId) => RemoveUpcoming(track => track.Id == trackId);

    public int RemoveUpcomingByAlbum(long albumId) => RemoveUpcoming(track => track.AlbumId == albumId);

    public int RemoveUpcomingByArtist(long artistId) => RemoveUpcoming(track => track.ArtistId == artistId);

    public int RemoveUpcomingByGenre(long genreId) => RemoveUpcoming(track => track.GenreId == genreId);

    private int CountUpcoming(Func<TrackDto, bool> predicate)
    {
        if (_mode == EPlaybackMode.Radio)
            return 0;

        int count = 0;

        for (int index = _currentIndex + 1; index < Playlist.Count; index++)
        {
            if (predicate(Playlist[index]))
                count++;
        }

        return count;
    }

    private int RemoveUpcoming(Func<TrackDto, bool> predicate)
    {
        if (_mode == EPlaybackMode.Radio)
            return 0;

        int removed = 0;
        bool imminentRemoved = false;

        lock (_transitionLock)
        {
            for (int index = Playlist.Count - 1; index > _currentIndex; index--)
            {
                if (!predicate(Playlist[index]))
                    continue;

                if (index == _currentIndex + 1)
                    imminentRemoved = true;

                Playlist.RemoveAt(index);
                removed++;
            }
        }

        if (removed == 0)
            return 0;

        if (imminentRemoved)
        {
            InvalidatePendingTransition();
            RequestMixPreparation();
        }

        _messenger.Send(new PlaylistChanged(Playlist));

        return removed;
    }

    private void CancelCrossfade()
    {
        lock (_transitionLock)
        {
            _crossfadeGeneration++;
            _isCrossfadeRunning = false;
        }

        _crossfadeCts?.Cancel();
    }

    public void AddTracksToPlaylist(List<TrackDto> tracks)
    {
        Guard.NotNull(tracks);

        bool hasTracks;
        TrackDto? imminentBefore;

        lock (_transitionLock)
        {
            hasTracks = Playlist.Count > 0;
            imminentBefore = PeekNextLocked();

            Playlist.AddRange(tracks);
        }

        if (!hasTracks)
            Start();
        else
            InvalidateIfImminentChanged(imminentBefore);

        _messenger.Send(new PlaylistChanged(Playlist));
    }

    public void InsertTracksToPlaylist(List<TrackDto> tracks, int? index = null)
    {
        Guard.NotNull(tracks);

        if (tracks.Count == 0)
            return;

        List<TrackDto> itemsToInsert = new(tracks.Count);
        itemsToInsert.AddRange(tracks);

        TrackDto? imminentBefore;

        lock (_transitionLock)
        {
            index ??= _currentIndex + 1;
            index = Math.Clamp(index.Value, 0, Playlist.Count);

            imminentBefore = PeekNextLocked();

            Playlist.InsertRange(index.Value, itemsToInsert);
        }

        InvalidateIfImminentChanged(imminentBefore);

        _messenger.Send(new PlaylistChanged(Playlist));
    }

    public void Start(TrackDto? startTrack = null)
    {
        TrackDto? track;

        lock (_transitionLock)
        {
            _currentIndex = startTrack == null ? 0 : Playlist.FindIndex(c => c.Id == startTrack.Id);
            track = _currentIndex >= 0 && _currentIndex < Playlist.Count ? Playlist[_currentIndex] : null;
        }

        if (track == null)
            return;

        LoadFile(track);

        Play();
    }

    public void Pause() => Pause(PauseReason.User);

    private void Pause(PauseReason reason)
    {
        StopSmtcTimelineTimer();

        PlaybackState = EPlaybackState.Paused;
        _pauseReason = reason;
        _pauseTimestampUtc = _timeProvider.GetUtcNow().UtcDateTime;

        CancelCrossfade();
        _player.Pause();

        _discordService?.ClearPresence();
        _smtcService?.UpdatePlaybackState(PlaybackStatus.Paused);
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
                _ = _smtcService?.UpdateTrackInfoAsync(CurrentTrack, ResolveCoverPath(CurrentTrack));
                _smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);
                StartSmtcTimelineTimer();
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
        StopSmtcTimelineTimer();

        CancelCrossfade();
        CancelMixPreparation(out _);
        ResetPendingTransition();
        _player.Stop();

        _mode = EPlaybackMode.None;
        _currentStation = null;
        _currentStreamTitle = null;

        if (firePlaybackStateChange)
            PlaybackState = EPlaybackState.Stopped;

        _pauseReason = PauseReason.None;

        _discordService?.ClearPresence();
        _smtcService?.UpdatePlaybackState(PlaybackStatus.Paused);
    }

    public void Skip()
    {
        Next();
    }

    public void Next()
    {
        if (_mode == EPlaybackMode.Radio)
            return;

        // Cancel any ongoing crossfade
        CancelCrossfade();

        TrackDto? track = null;

        lock (_transitionLock)
        {
            if (_currentIndex + 1 < Playlist.Count)
            {
                _currentIndex++;
                track = Playlist[_currentIndex];
            }
            else if (IsLoopingEnabled && Playlist.Count > 0)
            {
                _currentIndex = 0;
                track = Playlist[0];
            }
        }

        if (track == null)
        {
            // Playlist ended
            PlaybackState = EPlaybackState.Stopped;
            _smtcService?.UpdatePlaybackState(PlaybackStatus.Stopped);
            StopSmtcTimelineTimer();
            return;
        }

        LoadFile(track);
        Play();
    }

    public void Previous()
    {
        if (_mode == EPlaybackMode.Radio)
            return;

        TrackDto? track = null;

        lock (_transitionLock)
        {
            if (_currentIndex - 1 >= 0)
            {
                _currentIndex--;
                track = Playlist[_currentIndex];
            }
            else if (IsLoopingEnabled && Playlist.Count > 0)
            {
                _currentIndex = Playlist.Count - 1;
                track = Playlist[_currentIndex];
            }
        }

        if (track == null)
        {
            PlaybackState = EPlaybackState.Stopped;
            return;
        }

        LoadFile(track);
        Play();
    }

    public void ShuffleTracks()
    {
        TrackDto? imminentBefore;

        lock (_transitionLock)
        {
            imminentBefore = PeekNextLocked();

            TracksRandomizer.ArtistBalancedTrackRandomize(Playlist, _currentIndex);
        }

        InvalidateIfImminentChanged(imminentBefore);

        _messenger.Send(new PlaylistChanged(Playlist));
    }

    public void PlayRadioStation(RadioStationDto station)
    {
        Guard.NotNull(station);

        StopForModeSwitch();

        lock (_transitionLock)
        {
            Playlist.Clear();
        }

        _currentTrack = null;
        _currentStation = station;
        _currentStreamTitle = null;
        _mode = EPlaybackMode.Radio;

        if (!_player.SetStream(station))
        {
            _currentStation = null;
            _mode = EPlaybackMode.None;
            PlaybackState = EPlaybackState.Stopped;
            return;
        }

        PlaybackState = EPlaybackState.Playing;
        _messenger.Send(new RadioStationChanged(station));
        _smtcService?.UpdateRadioStation(station);
        _smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);
        _discordService?.UpdateRadioStation(station);
    }

    private void StopForModeSwitch()
    {
        if (_mode == EPlaybackMode.Radio)
        {
            CancelMixPreparation(out _);
            _player.Stop();
            _currentStation = null;
            _currentStreamTitle = null;
            _lastIsBuffering = false;
        }
    }

    #region Engine

    private void LoadFile(TrackDto track)
    {
        long durationPlayed = (long)_player.Position;
        ResetPendingTransition();
        _player.Stop();

        bool res = _player.SetTrack(track, ResolveCurrentReplayGain());

        if (res)
        {
            TrackDto? previousTrack = CurrentTrack;
            CurrentTrack = track;

            if ((previousTrack == null || previousTrack.Id != _currentTrack?.Id) && _currentTrack != null)
                _messenger.Send(new MediaChangedMessage(_currentTrack, previousTrack, durationPlayed));

            UpdateDiscordPresence(track, isPlaying: false);
            RequestMixPreparation();
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
                    duration: TimeSpan.FromSeconds(track.Duration)
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

    private async Task CrossfadeToNextTrackAsync(MixPlan? mixPlan)
    {
        long generation;
        bool handled = false;

        lock (_transitionLock)
        {
            generation = _crossfadeGeneration;
        }

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

            if (!TryPeekNext(out int nextIndex, out TrackDto? nextTrack))
                return;

            double trackLength = _player.Length;
            double currentPosition = _player.Position;

            if (mixPlan != null)
                handled = await RunMixCrossfadeAsync(mixPlan, nextIndex, nextTrack, cancellationToken);
            else
                handled = await RunCrossfadeAsync(nextIndex, nextTrack, trackLength, currentPosition, cancellationToken);
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
            bool fallback = false;

            lock (_transitionLock)
            {
                if (generation == _crossfadeGeneration)
                {
                    _isCrossfadeRunning = false;
                    fallback = !handled && _outgoingEndedDuringCrossfade;
                    _outgoingEndedDuringCrossfade = false;
                }
            }

            if (_crossfadeCts != null && _crossfadeCts.IsCancellationRequested)
            {
                _crossfadeCts.Dispose();
                _crossfadeCts = null;
            }

            if (fallback)
            {
                _logger.LogDebug("Outgoing track ended during a failed crossfade, advancing");
                Next();
            }
        }
    }

    private bool TryGetNextIndexLocked(out int nextIndex)
    {
        nextIndex = _currentIndex + 1;

        if (nextIndex < Playlist.Count)
            return true;

        if (!IsLoopingEnabled)
            return false;

        nextIndex = 0;
        return true;
    }

    private async Task<bool> RunCrossfadeAsync(int nextIndex, TrackDto nextTrack, double trackLength, double positionAtDecisionTime, CancellationToken cancellationToken)
    {
        double remainingTime = Math.Max(0, trackLength - positionAtDecisionTime);
        double crossfadeDurationSeconds = Math.Min(Math.Min(CrossfadeDuration.Clamp(_appOptions.CrossfadeDurationSeconds), remainingTime), trackLength / 2);

        if (crossfadeDurationSeconds <= 0)
        {
            _logger.LogDebug("No crossfade possible, remaining time: {RemainingTime}s", remainingTime);

            if (remainingTime > 0)
                await Task.Delay(TimeSpan.FromSeconds(remainingTime), cancellationToken);

            Next();

            return true;
        }

        double freshPosition = _player.Position;
        double timeToWaitBeforeCrossfade = Math.Max(0, trackLength - freshPosition - crossfadeDurationSeconds);

        if (timeToWaitBeforeCrossfade > 0)
            await Task.Delay(TimeSpan.FromSeconds(timeToWaitBeforeCrossfade), cancellationToken);

        _logger.LogDebug("Starting simultaneous crossfade to {Track} over {Duration}s", nextTrack.Title, crossfadeDurationSeconds);

        long durationPlayed = (long)_player.Position;

        bool started = await _player.CrossfadeToAsync(nextTrack, ResolveReplayGain(nextIndex), crossfadeDurationSeconds, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!started)
        {
            _logger.LogWarning("Crossfade to {Track} failed, {Current} keeps playing", nextTrack.Title, CurrentTrack?.Title);

            return false;
        }

        await AdvanceToWithoutLoadAsync(nextIndex, nextTrack, durationPlayed);

        return true;
    }

    private async Task<bool> RunMixCrossfadeAsync(MixPlan plan, int nextIndex, TrackDto nextTrack, CancellationToken cancellationToken)
    {
        if (plan.IncomingTrackId != nextTrack.Id)
            return false;

        double position = _player.Position;
        (double duration, double incomingStart, double bassSwapAt) = MixTransitionPlanner.ResolveAt(plan, position);

        _logger.LogDebug(
            "Starting mix to {Track} over {Duration}s from {Start}s, bass swap at {BassSwapAt}s, stretch {Stretch}, bars {Bars}",
            nextTrack.Title,
            duration,
            incomingStart,
            bassSwapAt,
            plan.Stretch?.Ratio.ToString("0.000", CultureInfo.InvariantCulture) ?? "none",
            plan.Stretch?.Bars ?? 0);

        MixTransition transition = new(
            incomingStart,
            BassSwap: true,
            BassSwapAtSeconds: bassSwapAt,
            Stretch: plan.Stretch,
            ReferencePositionSeconds: plan.Alignment != null ? position : null);

        long durationPlayed = (long)position;

        bool started = await _player.CrossfadeToAsync(nextTrack, ResolveReplayGain(nextIndex), duration, transition, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!started)
        {
            _logger.LogWarning("Mix to {Track} failed, {Current} keeps playing", nextTrack.Title, CurrentTrack?.Title);

            return false;
        }

        await AdvanceToWithoutLoadAsync(nextIndex, nextTrack, durationPlayed);

        return true;
    }

    private async Task AdvanceToWithoutLoadAsync(int index, TrackDto track, long durationPlayed)
    {
        TrackDto? previousTrack = CurrentTrack;

        lock (_transitionLock)
        {
            _currentIndex = index;
        }

        CurrentTrack = track;

        if (previousTrack == null || previousTrack.Id != track.Id)
            _messenger.Send(new MediaChangedMessage(track, previousTrack, durationPlayed));

        RequestMixPreparation();

        PlaybackState = EPlaybackState.Playing;
        UpdateDiscordPresence(track, isPlaying: true);
        await (_smtcService?.UpdateTrackInfoAsync(track, ResolveCoverPath(track)) ?? Task.CompletedTask);
        _smtcService?.UpdatePlaybackState(PlaybackStatus.Playing);
    }

    private string? ResolveCoverPath(TrackDto track)
    {
        if (string.IsNullOrEmpty(track.MusicFile))
            return null;

        string? albumDir = Path.GetDirectoryName(track.MusicFile);

        if (string.IsNullOrEmpty(albumDir))
            return null;

        return _albumPicture.PictureFileExists(albumDir)
            ? _albumPicture.GetPictureFile(albumDir)
            : null;
    }

    private void StartSmtcTimelineTimer()
    {
        _smtcTimelineTimer?.Dispose();
        _smtcTimelineTimer = _timeProvider.CreateTimer(
            _ => OnSmtcTimelineTick(),
            state: null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1));
    }

    private void StopSmtcTimelineTimer()
    {
        _smtcTimelineTimer?.Dispose();
        _smtcTimelineTimer = null;
    }

    public void Dispose()
    {
        _replayGainSubscription.Dispose();
        _player.OnTransitionCue -= OnTransitionCue;
        CancelMixPreparation(out _);
        _smtcTimelineTimer?.Dispose();
        _smtcTimelineTimer = null;
        _crossfadeCts?.Dispose();
        _crossfadeCts = null;
    }

    private float ResolveReplayGain(int index)
    {
        lock (_transitionLock)
        {
            return ResolveReplayGainLocked(index);
        }
    }

    private float ResolveCurrentReplayGain()
    {
        lock (_transitionLock)
        {
            return ResolveReplayGainLocked(_currentIndex);
        }
    }

    private float ResolveReplayGainLocked(int index)
    {
        if (index < 0 || index >= Playlist.Count)
            return 1f;

        TrackDto? previous = index > 0 ? Playlist[index - 1] : null;
        TrackDto? next = index + 1 < Playlist.Count ? Playlist[index + 1] : null;

        return ReplayGainCalculator.Resolve(_appOptions.ReplayGainMode, _appOptions.ReplayGainPreampDb, previous, Playlist[index], next);
    }

    private void ApplyReplayGainOptions()
    {
        if (_mode == EPlaybackMode.Radio || CurrentTrack == null)
            return;

        TrackDto current = CurrentTrack;
        float currentGain;
        TrackDto? next = null;
        float nextGain = 1f;

        lock (_transitionLock)
        {
            currentGain = ResolveReplayGainLocked(_currentIndex);

            if (TryGetNextIndexLocked(out int nextIndex))
            {
                next = Playlist[nextIndex];
                nextGain = ResolveReplayGainLocked(nextIndex);
            }
        }

        _player.UpdateReplayGain(current.Id, currentGain);

        if (next != null)
            _player.UpdateReplayGain(next.Id, nextGain);
    }

    private void OnSmtcTimelineTick()
    {
        if (_smtcService is null || CurrentTrack is null)
            return;

        TimeSpan position = TimeSpan.FromSeconds(_player.Position);
        TimeSpan duration = TimeSpan.FromSeconds(CurrentTrack.Duration);

        _smtcService.UpdateTimeline(position, duration);
    }

    #endregion
}