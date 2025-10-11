using Rok.Application.Dto.Lyrics;
using Rok.Application.Features.Playlists.PlaylistMenu;
using Rok.Application.Features.Tracks.Command;
using Rok.Infrastructure.NovaApi;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Tracks;

public partial class TrackViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<TrackViewModel> _logger;

    private readonly ResourceLoader _resourceLoader;

    public TrackDto Track { get; private set; } = new();

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

    public RelayCommand AlbumOpenCommand { get; init; }
    public RelayCommand ArtistOpenCommand { get; init; }
    public RelayCommand TrackOpenCommand { get; init; }
    public AsyncRelayCommand LyricsOpenCommand { get; init; }
    public RelayCommand ListenCommand { get; init; }


    public TrackViewModel(IPlaylistMenuService playlistMenuService, IMediator mediator, NavigationService navigationService, ResourceLoader resourceLoader, IDialogService dialogService, IPlayerService playerService, INovaApiService novaApiService, ILyricsService lyricsService, ILogger<TrackViewModel> logger)
    {
        PlaylistMenuService = playlistMenuService;
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
        // TODO
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

            OnPropertyChanged(nameof(LyricsExists));
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
