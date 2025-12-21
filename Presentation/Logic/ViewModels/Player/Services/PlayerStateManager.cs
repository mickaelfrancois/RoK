using Microsoft.UI.Dispatching;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Player;
using Rok.Logic.ViewModels.Albums;
using Rok.Logic.ViewModels.Artists;
using Rok.Logic.ViewModels.Tracks;

namespace Rok.Logic.ViewModels.Player.Services;

public partial class PlayerStateManager : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;

    private TrackViewModel? _currentTrack;
    public TrackViewModel? CurrentTrack
    {
        get => _currentTrack;
        set
        {
            _currentTrack = value;
            OnPropertyChanged();
        }
    }

    private ArtistViewModel? _currentArtist;
    public ArtistViewModel? CurrentArtist
    {
        get => _currentArtist;
        set
        {
            _currentArtist = value;
            OnPropertyChanged();
        }
    }

    private AlbumViewModel? _currentAlbum;
    public AlbumViewModel? CurrentAlbum
    {
        get => _currentAlbum;
        set
        {
            _currentAlbum = value;
            OnPropertyChanged();
        }
    }

    private bool _canSkipNext;
    public bool CanSkipNext
    {
        get => _canSkipNext;
        set
        {
            if (_canSkipNext != value)
            {
                _canSkipNext = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _canSkipPrevious;
    public bool CanSkipPrevious
    {
        get => _canSkipPrevious;
        set
        {
            if (_canSkipPrevious != value)
            {
                _canSkipPrevious = value;
                OnPropertyChanged();
            }
        }
    }

    private EPlaybackState _playbackState = EPlaybackState.Stopped;
    public EPlaybackState PlaybackState
    {
        get => _playbackState;
        set
        {
            _playbackState = value;
            OnPropertyChanged();
        }
    }

    private LyricsModel? _lyrics = new();
    public LyricsModel? Lyrics => _lyrics;

    private int _lyricsCurrentIndex = 0;

    public bool LyricsExist { get; set; } = false;

    public SyncLyricsModel SyncLyrics { get; set; } = new();

    private LyricLine _currentLyric = new();
    public LyricLine CurrentLyric
    {
        get => _currentLyric;
        set
        {
            _currentLyric = value;
            OnPropertyChanged();
        }
    }

    private string _previousLyrics = string.Empty;
    public string PreviousLyrics
    {
        get => _previousLyrics;
        set
        {
            _previousLyrics = value;
            OnPropertyChanged();
        }
    }

    private string _nextLyrics = string.Empty;
    public string NextLyrics
    {
        get => _nextLyrics;
        set
        {
            _nextLyrics = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<LyricLine> LyricsLines => SyncLyrics.Lyrics;

    public bool IsSynchronizedLyrics => _lyrics?.LyricsType == ELyricsType.Synchronized;

    public string? PlainLyrics => _lyrics?.PlainLyrics;


    public PlayerStateManager(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void SetLyrics(LyricsModel? lyrics)
    {
        _lyrics = lyrics;
        OnPropertyChanged(nameof(Lyrics));
        OnPropertyChanged(nameof(LyricsExist));
        OnPropertyChanged(nameof(IsSynchronizedLyrics));
        OnPropertyChanged(nameof(PlainLyrics));
    }

    public void SetSyncLyrics(SyncLyricsModel syncLyrics)
    {
        SyncLyrics = syncLyrics;
        OnPropertyChanged(nameof(LyricsLines));
    }

    public void ResetLyrics()
    {
        _lyrics = new();
        SyncLyrics = new();
        SyncLyrics.Lyrics.Clear();
        _lyricsCurrentIndex = -1;
        CurrentLyric = new();
        PreviousLyrics = string.Empty;
        NextLyrics = string.Empty;

        OnPropertyChanged(nameof(CurrentLyric));
        OnPropertyChanged(nameof(LyricsLines));
        OnPropertyChanged(nameof(LyricsExist));
    }

    public void UpdateLyricsTime(TimeSpan time)
    {
        if (SyncLyrics == null)
            return;

        int start = _lyricsCurrentIndex + 1;

        for (int i = start; i < SyncLyrics.Time.Count; i++)
        {
            if (SyncLyrics.Time[i] > time)
            {
                _lyricsCurrentIndex = i - 1;
                break;
            }
        }

        if (_lyricsCurrentIndex < 0 || _lyricsCurrentIndex >= LyricsLines.Count)
            CurrentLyric = new();
        else
        {
            PreviousLyrics = _lyricsCurrentIndex - 1 >= 0 ? LyricsLines[_lyricsCurrentIndex - 1].Lyric : string.Empty;
            CurrentLyric = LyricsLines[_lyricsCurrentIndex];
            NextLyrics = _lyricsCurrentIndex + 1 < LyricsLines.Count ? LyricsLines[_lyricsCurrentIndex + 1].Lyric : string.Empty;
        }
    }

    public void ExecuteOnUIThread(Action action)
    {
        _dispatcherQueue.TryEnqueue(() => action());
    }
}