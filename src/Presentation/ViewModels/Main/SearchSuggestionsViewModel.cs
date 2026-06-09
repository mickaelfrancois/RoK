using CommunityToolkit.Mvvm.ComponentModel;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Services;

namespace Rok.ViewModels.Main;

public sealed partial class SearchSuggestionsViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly IDisposable _libraryRefreshSubscription;
    private System.Threading.CancellationTokenSource? _debounceCts;
    private bool _isCacheLoaded;
    private bool _disposed;

    public ObservableCollection<AlbumDto> AlbumSuggestions { get; } = new ObservableCollection<AlbumDto>();
    public ObservableCollection<ArtistDto> ArtistSuggestions { get; } = new ObservableCollection<ArtistDto>();
    public ObservableCollection<TrackDto> TrackSuggestions { get; } = new ObservableCollection<TrackDto>();

    private IEnumerable<ArtistDto>? _artistsCache;
    private IEnumerable<AlbumDto>? _albumsCache;
    private IEnumerable<TrackDto>? _tracksCache;

    private bool _hasResults;
    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public SearchSuggestionsViewModel(IMediator mediator, IMessenger messenger)
    {
        _mediator = mediator;
        _libraryRefreshSubscription = messenger.Subscribe<LibraryRefreshMessage>(OnLibraryRefresh);
    }

    private void OnLibraryRefresh(LibraryRefreshMessage message)
    {
        if (message.ProcessState == LibraryRefreshMessage.EState.Stop)
            _isCacheLoaded = false;
    }


    public async Task UpdateSuggestionsAsync(string keyword)
    {
        if (_debounceCts != null)
        {
            await _debounceCts.CancelAsync();
            _debounceCts.Dispose();
        }
        _debounceCts = new System.Threading.CancellationTokenSource();


        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
        {
            ClearSuggestions();
            return;
        }

        try
        {
            await Task.Delay(300, _debounceCts.Token);

            if (!_isCacheLoaded)
                await LoadCacheAsync();

            string lowerKeyword = keyword.ToLowerInvariant();

            AlbumSuggestions.Clear();
            ArtistSuggestions.Clear();
            TrackSuggestions.Clear();

            foreach (AlbumDto album in _albumsCache!
                .Where(a => a.Name.ToLowerInvariant().Contains(lowerKeyword) || Levenshtein.ComputeLevenshtein(a.Name.ToLowerInvariant(), lowerKeyword) <= Levenshtein.GetThreshold(lowerKeyword))
                .Take(3))
                AlbumSuggestions.Add(album);

            foreach (ArtistDto artist in _artistsCache!
                .Where(a => a.Name.ToLowerInvariant().Contains(lowerKeyword) || Levenshtein.ComputeLevenshtein(a.Name.ToLowerInvariant(), lowerKeyword) <= Levenshtein.GetThreshold(lowerKeyword))
                .Take(3))
                ArtistSuggestions.Add(artist);

            foreach (TrackDto track in _tracksCache!
                .Where(a => a.Title.ToLowerInvariant().Contains(lowerKeyword) || Levenshtein.ComputeLevenshtein(a.Title.ToLowerInvariant(), lowerKeyword) <= Levenshtein.GetThreshold(lowerKeyword))
                .Take(3))
                TrackSuggestions.Add(track);

            HasResults = AlbumSuggestions.Count > 0 || ArtistSuggestions.Count > 0 || TrackSuggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation, it's expected when the user types quickly
        }
    }


    public void ClearSuggestions()
    {
        AlbumSuggestions.Clear();
        ArtistSuggestions.Clear();
        TrackSuggestions.Clear();
        HasResults = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _libraryRefreshSubscription.Dispose();
        _debounceCts?.Dispose();
        _disposed = true;
    }


    private async Task LoadCacheAsync()
    {
        _artistsCache = await _mediator.Send(new GetAllArtistsRequest());
        _albumsCache = await _mediator.Send(new GetAllAlbumsRequest());
        _tracksCache = await _mediator.Send(new GetAllTracksRequest());
        _isCacheLoaded = true;
    }
}