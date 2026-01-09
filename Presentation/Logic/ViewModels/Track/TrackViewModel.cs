using Microsoft.UI.Dispatching;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Infrastructure.Translate;
using Rok.Logic.Services.Player;
using Rok.Logic.ViewModels.Track.Services;

namespace Rok.Logic.ViewModels.Tracks;

public partial class TrackViewModel : ObservableObject, IDisposable
{
    private readonly ResourceLoader _resourceLoader;
    private readonly IPlayerService _playerService;
    private readonly IDialogService _dialogService;
    private readonly IBackdropLoader _backdropLoader;
    private readonly IAppOptions _appOptions;
    private readonly TrackDetailDataLoader _dataLoader;
    private readonly TrackLyricsService _lyricsService;
    private readonly TrackScoreService _scoreService;
    private readonly TrackNavigationService _navigationService;

    public TrackDto Track { get; private set; } = new();

    public string BitrateStr
    {
        get
        {
            int bitRate = Track.Bitrate;

            if (bitRate > 1000)
                return $"{Track.Bitrate / 1000} kbps";
            else if (bitRate > 0)
                return $"{bitRate} bps";
            else
                return "";
        }
    }

    public string SizeStr
    {
        get
        {
            long size = Track.Size;

            if (size >= 1073741824)
                return $"{size / 1073741824.0:F2} GB";
            else if (size >= 1048576)
                return $"{size / 1048576.0:F2} MB";
            else if (size >= 1024)
                return $"{size / 1024.0:F2} KB";
            else if (size > 0)
                return $"{size} B";
            else
                return "";
        }
    }

    public string DurationStr
    {
        get
        {
            TimeSpan time = TimeSpan.FromSeconds(Track.Duration);
            return time.Hours > 0 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
        }
    }

    public int? TrackNumber => Track.TrackNumber;

    public string TrackNumberStr
    {
        get
        {
            if (!Track.TrackNumber.HasValue)
                return "00";

            return Track.TrackNumber.Value.ToString().PadLeft(2, '0') + ".";
        }
    }

    public string Title
    {
        get
        {
            if (string.IsNullOrEmpty(Track.Title))
                return _resourceLoader.GetString("noTitle");
            else
                return Track.Title;
        }
    }

    public int Score
    {
        get
        {
            if (Track.Score == 0)
                return -1;
            else
                return Track.Score;
        }
        set
        {
            if (Track.Score == value || (Track.Score == 0 && value == -1))
                return;

            if (value == -1)
                Track.Score = 0;
            else
                Track.Score = value;

            OnPropertyChanged(nameof(Score));
#pragma warning disable 4014
            SetScoreAsync(Track.Score);
#pragma warning restore 4014
        }
    }

    public string AlbumName => Track.AlbumName;
    public string ArtistName => Track.ArtistName;

    private LyricsModel? _lyrics;

    public string PlainLyrics => _lyrics?.PlainLyrics ?? string.Empty;

    public bool LyricsExists
    {
        get
        {
            bool exists = _lyricsService.CheckLyricsExists(Track.MusicFile);
            if (!exists)
            {
                // Avoid fire-and-forget by explicitly handling the task
                _ = GetLyricsFromAPIAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        // Log the exception if needed
                        // _logger.LogError(task.Exception, "Failed to fetch lyrics from API");
                    }
                }, TaskScheduler.Default);
            }

            return exists;
        }
    }

    public IPlaylistMenuService PlaylistMenuService { get; }

    public BitmapImage Picture { get; set; } = null!;

    private BitmapImage? _backdrop = null;
    public BitmapImage? Backdrop
    {
        get => _backdrop;
        set
        {
            _backdrop = value;
            OnPropertyChanged();
        }
    }

    private bool _listening = false;
    public bool Listening
    {
        get => _listening;
        set
        {
            if (_listening != value)
            {
                _listening = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Listened { get; set; }

    public RelayCommand AlbumOpenCommand { get; private set; }
    public RelayCommand GenreOpenCommand { get; private set; }
    public RelayCommand ArtistOpenCommand { get; private set; }
    public RelayCommand TrackOpenCommand { get; private set; }
    public AsyncRelayCommand LyricsOpenCommand { get; private set; }
    public RelayCommand ListenCommand { get; private set; }

    public TrackViewModel(
        IBackdropLoader backdropLoader,
        IPlaylistMenuService playlistMenuService,
        ResourceLoader resourceLoader,
        IDialogService dialogService,
        IPlayerService playerService,
        TrackDetailDataLoader dataLoader,
        TrackLyricsService lyricsService,
        TrackScoreService scoreService,
        TrackNavigationService navigationService,
        IAppOptions appOptions,
        ILogger<TrackViewModel> logger)
    {
        _backdropLoader = Guard.Against.Null(backdropLoader);
        PlaylistMenuService = Guard.Against.Null(playlistMenuService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dialogService = Guard.Against.Null(dialogService);
        _playerService = Guard.Against.Null(playerService);
        _dataLoader = Guard.Against.Null(dataLoader);
        _lyricsService = Guard.Against.Null(lyricsService);
        _scoreService = Guard.Against.Null(scoreService);
        _navigationService = Guard.Against.Null(navigationService);
        _appOptions = Guard.Against.Null(appOptions);

        ArtistOpenCommand = new RelayCommand(ArtistOpen);
        AlbumOpenCommand = new RelayCommand(AlbumOpen);
        TrackOpenCommand = new RelayCommand(TrackOpen);
        LyricsOpenCommand = new AsyncRelayCommand(LyricsOpenAsync);
        ListenCommand = new RelayCommand(Listen);
        GenreOpenCommand = new RelayCommand(GenreOpen);

        SubscribeToMessages();
    }

    private void SubscribeToMessages()
    {
        Messenger.Subscribe<TrackScoreUpdateMessage>(TrackScoreUpdateMessageHandle);
    }

    public async Task LoadDataAsync(long trackId)
    {
        TrackDto? track = await _dataLoader.LoadTrackAsync(trackId);
        if (track == null)
            return;

        Track = track;
        LoadBackdrop();

        OnPropertyChanged(string.Empty);
    }

    public void SetData(TrackDto track)
    {
        Track = track;
    }

    private void TrackScoreUpdateMessageHandle(TrackScoreUpdateMessage message)
    {
        if (message.TrackId != Track.Id)
            return;

        Track.Score = message.Score;
        OnPropertyChanged(nameof(Score));
    }

    private async Task SetScoreAsync(int score)
    {
        await _scoreService.UpdateScoreAsync(Track.Id, score);
    }

    private void ArtistOpen()
    {
        if (Track.ArtistId.HasValue)
            _navigationService.NavigateToArtist(Track.ArtistId);
    }

    private void AlbumOpen()
    {
        if (Track.AlbumId.HasValue)
            _navigationService.NavigateToAlbum(Track.AlbumId);
    }

    private void GenreOpen()
    {
        if (Track.GenreId.HasValue)
            _navigationService.NavigateToGenre(Track.GenreId);
    }

    private void TrackOpen()
    {
        _navigationService.NavigateToTrack(Track.Id);
    }

    private async Task LyricsOpenAsync()
    {
        await LoadLyricsAsync();

        if (!string.IsNullOrEmpty(PlainLyrics))
        {
            string? rawLanguage = Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault();
            string language = TranslateService.NormalizeLanguageForLibreTranslate(rawLanguage, "fr");

            await _dialogService.ShowTextAsync($"{ArtistName} - {Title}", PlainLyrics, showTranslateButton: _appOptions.NovaApiEnabled, language);
        }
    }



    public async Task LoadLyricsAsync()
    {
        if (LyricsExists)
        {
            _lyrics = await _lyricsService.LoadLyricsAsync(Track.MusicFile);
            OnPropertyChanged(nameof(PlainLyrics));
        }
    }

    private void Listen()
    {
        if (Track != null)
            _playerService.LoadPlaylist([Track]);
    }

    private async Task GetLyricsFromAPIAsync()
    {
        bool success = await _lyricsService.GetAndSaveLyricsFromApiAsync(Track);

        if (success)
        {
            Track.GetLyricsLastAttempt = DateTime.UtcNow;

            DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => OnPropertyChanged(nameof(LyricsExists)));
            }
            else
            {
                OnPropertyChanged(nameof(LyricsExists));
            }
        }
    }

    public void LoadBackdrop()
    {
        if (Track.Id <= 0)
            return;

        _backdropLoader.LoadBackdrop(Track.ArtistName, (BitmapImage? backdropImage) =>
        {
            Backdrop = backdropImage;
        });
    }

    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}