using Microsoft.UI.Dispatching;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Tracks.Command;
using Rok.Application.Features.Tracks.Query;
using Rok.Infrastructure.NovaApi;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Tracks;

public partial class TrackViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<TrackViewModel> _logger;

    private readonly ResourceLoader _resourceLoader;

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

    public int? TrackNumber { get { return Track.TrackNumber; } }

    public string TrackNumberStr
    {
        get
        {
            if (Track.TrackNumber.HasValue == false)
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

    public string PlainLyrics
    {
        get
        {
            return _lyrics?.PlainLyrics ?? string.Empty;
        }
    }

    public bool LyricsExists
    {
        get
        {
            bool exists = _lyricsService.CheckLyricsFileExists(Track.MusicFile) != ELyricsType.None;
            if (!exists)
                _ = GetLyricsFromAPIAsync();

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
        get { return _listening; }
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

    private readonly NavigationService _navigationService;

    private readonly IMediator _mediator;

    private readonly IPlayerService _playerService;

    private readonly ILyricsService _lyricsService;

    private readonly INovaApiService _novaApiService;

    private readonly IDialogService _dialogService;

    private readonly BackdropPicture _backdropPicture;

    public RelayCommand AlbumOpenCommand { get; init; }
    public RelayCommand ArtistOpenCommand { get; init; }
    public RelayCommand TrackOpenCommand { get; init; }
    public AsyncRelayCommand LyricsOpenCommand { get; init; }
    public RelayCommand ListenCommand { get; init; }


    public TrackViewModel(BackdropPicture backdropPicture, IPlaylistMenuService playlistMenuService, IMediator mediator, NavigationService navigationService, ResourceLoader resourceLoader, IDialogService dialogService, IPlayerService playerService, INovaApiService novaApiService, ILyricsService lyricsService, ILogger<TrackViewModel> logger)
    {
        _backdropPicture = Guard.Against.Null(backdropPicture);
        PlaylistMenuService = Guard.Against.Null(playlistMenuService);
        _mediator = Guard.Against.Null(mediator);
        _navigationService = Guard.Against.Null(navigationService);
        _resourceLoader = Guard.Against.Null(resourceLoader);
        _dialogService = Guard.Against.Null(dialogService);
        _playerService = Guard.Against.Null(playerService);
        _novaApiService = Guard.Against.Null(novaApiService);
        _lyricsService = Guard.Against.Null(lyricsService);
        _logger = Guard.Against.Null(logger);

        ArtistOpenCommand = new RelayCommand(ArtistOpen);
        AlbumOpenCommand = new RelayCommand(AlbumOpen);
        TrackOpenCommand = new RelayCommand(TrackOpen);
        LyricsOpenCommand = new AsyncRelayCommand(LyricsOpenAsync);
        ListenCommand = new RelayCommand(Listen);

        Messenger.Subscribe<TrackScoreUpdateMessage>((message) => TrackScoreUpdateMessageHandle(message));
    }


    public async Task LoadDataAsync(long trackId)
    {
        Result<TrackDto> trackResult = await _mediator.SendMessageAsync(new GetTrackByIdQuery(trackId));
        if (trackResult.IsError)
            return;

        Track = trackResult.Value!;

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
        await _mediator.SendMessageAsync(new UpdateScoreCommand(Track.Id, score));
        Messenger.Send(new TrackScoreUpdateMessage(Track.Id, score));
    }


    private void ArtistOpen()
    {
        if (Track.ArtistId.HasValue)
            _navigationService.NavigateToArtist(Track.ArtistId.Value);
    }

    private void AlbumOpen()
    {
        if (Track.AlbumId.HasValue)
            _navigationService.NavigateToAlbum(Track.AlbumId.Value);
    }

    private void TrackOpen()
    {
        if (Track.Id > 0)
            _navigationService.NavigateToTrack(Track.Id);
    }

    private async Task LyricsOpenAsync()
    {
        await LoadLyricsAsync();

        if (!string.IsNullOrEmpty(PlainLyrics))
            await _dialogService.ShowTextAsync($"{ArtistName} - {Title}", PlainLyrics, _resourceLoader.GetString("Close"));
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
        if (string.IsNullOrEmpty(Track.MusicFile) || string.IsNullOrEmpty(Track.ArtistName) || string.IsNullOrEmpty(Track.Title))
            return;

        if (!NovaApiService.IsApiRetryAllowed(Track.GetLyricsLastAttempt))
            return;

        await _mediator.SendMessageAsync(new UpdateTrackGetLyricsLastAttemptCommand(Track.Id));

        _logger.LogTrace("Fetching lyrics for {Artist} - {Title} from API", Track.ArtistName, Track.Title);

        ApiLyricsModel? lyrics = await _novaApiService.GetLyricsAsync(ArtistName, Title);
        if (lyrics != null)
        {
            string fileName = lyrics.IsSynchronized ? _lyricsService.GetSynchronizedLyricsFileName(Track.MusicFile) : _lyricsService.GetPlainLyricsFileName(Track.MusicFile);

            await _lyricsService.SaveLyricsAsync(new LyricsModel
            {
                File = fileName,
                PlainLyrics = lyrics.Lyrics,
                LyricsType = lyrics.IsSynchronized ? ELyricsType.Synchronized : ELyricsType.Plain
            });

            _logger.LogTrace("Lyrics saved to {File}", fileName);


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

        try
        {
            string filePath;

            List<string> backdrops = _backdropPicture.GetBackdrops(Track.ArtistName);
            if (backdrops.Count > 0)
            {
                int index = Random.Shared.Next(backdrops.Count);
                filePath = backdrops[index];
            }
            else
            {
                filePath = _backdropPicture.GetRandomGenericBackdrop();
            }

            Backdrop = new BitmapImage(new Uri(filePath, UriKind.Absolute));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backdrop for artist: {ArtistName}", Track.ArtistName);
        }
    }


    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
            }

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
