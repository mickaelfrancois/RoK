using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Artists;

public partial class ArtistsViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly ArtistsGroupCategory _groupService;
    private readonly ArtistsFilter _filterService;
    private readonly IPlayerService _playerService;
    private readonly ILogger<ArtistsViewModel> _logger;
    private readonly IAppOptions _appOptions;

    public List<ArtistViewModel> ViewModels { get; set; } = [];

    private bool _stateLoaded = false;

    private bool _libraryUpdated = false;

    public RangeObservableCollection<ArtistsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    private List<ArtistViewModel> _filteredArtists = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public ObservableCollection<object> Selected { get; } = [];

    public List<ArtistViewModel> SelectedItems
    {
        get
        {
            List<ArtistViewModel> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (ArtistViewModel)c));

            return list;
        }
    }

    public int SelectedCount => Selected.Count;
    public bool IsSelectedItems => SelectedCount > 0;

    private int _totalCount = 0;
    public int TotalCount
    {
        get
        {
            return Selected.Count > 0 ? Selected.Count : _totalCount;
        }
        set
        {
            SetProperty(ref _totalCount, value);
        }
    }

    private string _groupByText = ArtistsGroupCategory.KGroupByArtist;
    public string GroupById => _groupByText;
    public string GroupByText
    {
        get
        {
            return _groupService.GetGroupByLabel(_groupByText);
        }
        set
        {
            _groupByText = value;
            OnPropertyChanged();
        }
    }

    private List<string> _selectedFilters = [];
    public List<string> SelectedFilters => _selectedFilters;
    private List<long> _selectedGenreFilters = [];
    public List<long> SelectedGenreFilters => _selectedGenreFilters;

    private string _filterByText = "";
    public string FilterByText
    {
        get => _filterByText;
        set
        {
            if (_filterByText != value)
            {
                _filterByText = value;
                OnPropertyChanged(nameof(FilterByText));
            }
        }
    }

    public RelayCommand<long?> FilterByGenreCommand { get; private set; }
    public RelayCommand<string> FilterByCommand { get; private set; }
    public RelayCommand<string> GroupByCommand { get; private set; }
    public AsyncRelayCommand ListenCommand { get; private set; }
    public AsyncRelayCommand<ArtistsGroupCategoryViewModel> ListenGroupCommand { get; private set; }



    public ArtistsViewModel(IMediator mediator, ArtistsFilter artistsFilter, ArtistsGroupCategory artistsGroupCategory, IPlayerService playerService, IAppOptions appOptions, ILogger<ArtistsViewModel> logger)
    {
        _mediator = mediator;
        _groupService = artistsGroupCategory;
        _filterService = artistsFilter;
        _playerService = playerService;
        _appOptions = appOptions;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenGroupCommand = new AsyncRelayCommand<ArtistsGroupCategoryViewModel>(ListenGroupAsync);
        ListenCommand = new AsyncRelayCommand(ListenAsync);

        Messenger.Subscribe<LibraryRefreshMessage>((message) => LibraryRefreshHandle(message));
        Messenger.Subscribe<ArtistUpdateMessage>(async (message) => await ArtistUpdatedMessageHandleAsync(message));
        Messenger.Subscribe<ArtistImportedMessage>((message) => _libraryUpdated = true);

        Selected.CollectionChanged += Selected_CollectionChanged;
    }


    private async Task ArtistUpdatedMessageHandleAsync(ArtistUpdateMessage message)
    {
        ArtistViewModel? artistToUpdate = ViewModels.FirstOrDefault(c => c.Artist.Id == message.Id);
        ArtistDto artistDto = default!;

        ActionType action = message.Action;

        if (action == ActionType.Add && artistToUpdate != null)
            action = ActionType.Update;

        if ((action == ActionType.Update || action == ActionType.Delete) && artistToUpdate == null)
        {
            _logger.LogWarning("Artist {Id} not found for update or delete.", message.Id);
            return;
        }

        if (action == ActionType.Update || action == ActionType.Add)
        {
            Result<ArtistDto> result = await _mediator.SendMessageAsync(new GetArtistByIdQuery(message.Id));
            if (result.IsError)
            {
                _logger.LogError("Failed to retrieve artist {Id} for update or delete: {ErrorMessage}", message.Id, result.Error);
                return;
            }
            else
                artistDto = result.Value!;
        }

        switch (action)
        {
            case ActionType.Add:
                ArtistViewModel viewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
                viewModel.SetData(artistDto);
                ViewModels.Add(viewModel);
                _logger.LogTrace("Artist {Id} viewmodel add.", message.Id);
                break;

            case ActionType.Update:
                artistToUpdate!.SetData(artistDto);
                _logger.LogTrace("Artist {Id} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                ViewModels.RemoveAll(c => c.Artist.Id == message.Id);
                _logger.LogTrace("Artist {Id} viewmodel removed.", message.Id);
                break;

            case ActionType.Picture:
                artistToUpdate!.LoadPicture();
                _logger.LogTrace("Artist {Id} picture updated.", message.Id);
                break;
        }

        _libraryUpdated = true;
    }


    private void LibraryRefreshHandle(LibraryRefreshMessage message)
    {
        if (message.Statistics.HasAnyImport)
        {
            _logger.LogInformation("Library updated with {Count} new items.", message.Statistics.TotalCount);
            _libraryUpdated = true;
        }
    }


    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (mustLoad == false)
        {
            _logger.LogInformation("Artists already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;

        if (!_stateLoaded)
            LoadState();

        await LoadGenresAsync();

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Artists loaded"))
        {
            IEnumerable<ArtistDto> artists = await _mediator.SendMessageAsync(new GetAllArtistsQuery() { ExcludeArtistsWithoutAlbum = _appOptions.HideArtistsWithoutAlbum });
            ViewModels = CreateArtistsViewModels(artists);

            FilterAndSort();
        }
    }


    public void SetData(List<ArtistDto> artists)
    {
        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Artists loaded"))
        {
            ViewModels = CreateArtistsViewModels(artists);

            FilterAndSort();
        }
    }


    private static List<ArtistViewModel> CreateArtistsViewModels(IEnumerable<ArtistDto> artists)
    {
        int capacity = artists.Count();
        List<ArtistViewModel> artistViewModels = new(capacity);

        foreach (ArtistDto artist in artists)
        {
            ArtistViewModel artistViewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
            artistViewModel.SetData(artist);
            artistViewModels.Add(artistViewModel);
        }

        return artistViewModels;
    }


    private async Task LoadGenresAsync()
    {
        IEnumerable<GenreDto> genres = await _mediator.SendMessageAsync(new GetAllGenresQuery());
        Genres.AddRange(genres.OrderBy(c => c.Name));
    }


    private void FilterBy(string filterBy)
    {
        if (string.IsNullOrEmpty(filterBy))
        {
            _selectedFilters.Clear();
            _selectedGenreFilters.Clear();
        }
        else if (_selectedFilters.Contains(filterBy))
            _selectedFilters.Remove(filterBy);
        else
            _selectedFilters.Add(filterBy);

        SetFilterLabel();
        FilterAndSort();
    }


    private void FilterByGenreId(long? id)
    {
        if (id == null)
            _selectedGenreFilters.Clear();
        else if (_selectedGenreFilters.Contains(id.Value))
            _selectedGenreFilters.Remove(id.Value);
        else
            _selectedGenreFilters.Add(id.Value);

        SetFilterLabel();
        FilterAndSort();
    }


    private void SetFilterLabel()
    {
        if (_selectedFilters.Count > 0)
            FilterByText = _filterService.GetLabel(_selectedFilters[_selectedFilters.Count - 1]);
        else if (_selectedGenreFilters.Count > 0)
            FilterByText = Genres.FirstOrDefault(c => c.Id == _selectedGenreFilters[_selectedGenreFilters.Count - 1])?.Name ?? "";
        else
            FilterByText = _filterService.GetLabel("");
    }


    private void GroupBy(string groupBy)
    {
        GroupByText = groupBy;
        FilterAndSort();
    }


    private void FilterAndSort()
    {
        IEnumerable<ArtistViewModel> filteredArtists = ViewModels;
        foreach (string filterby in _selectedFilters)
            filteredArtists = ArtistsFilter.Filter(filterby, filteredArtists);

        foreach (long genreId in _selectedGenreFilters)
            filteredArtists = ArtistsFilter.FilterByGenreId(genreId, filteredArtists);

        _filteredArtists = filteredArtists.ToList();

        IEnumerable<ArtistsGroupCategoryViewModel> artists = ArtistsGroupCategory.GetGroupedItems(_groupByText, _filteredArtists);
        GroupedItems.InitWithAddRange(artists);

        TotalCount = _filteredArtists.Count;
    }


    private void Selected_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedItems));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(IsSelectedItems));
        OnPropertyChanged(nameof(Selected));
    }


    public void SaveState()
    {
        _appOptions.ArtistsGroupBy = _groupByText;
        _appOptions.ArtistsFilterBy = _selectedFilters;
        _appOptions.ArtistsFilterByGenresId = _selectedGenreFilters;
    }


    private void LoadState()
    {
        _stateLoaded = true;
        _groupByText = string.IsNullOrEmpty(_appOptions.ArtistsGroupBy) ? ArtistsGroupCategory.KGroupByArtist : _appOptions.ArtistsGroupBy;
        _selectedFilters = _appOptions.ArtistsFilterBy;
        _selectedGenreFilters = _appOptions.ArtistsFilterByGenresId;
        SetFilterLabel();

        OnPropertyChanged(nameof(GroupByText));
        OnPropertyChanged(nameof(FilterByText));
    }


    private async Task ListenAsync()
    {
        List<long> artistIds = [];

        if (Selected.Count == 0)
            artistIds.AddRange(_filteredArtists.Select(artist => artist.Artist.Id));
        else
            artistIds.AddRange(SelectedItems.Select(artist => artist.Artist.Id));

        await ListenAsync(artistIds);
    }


    private async Task ListenAsync(List<long> artistIds)
    {
        if (artistIds.Count != 0)
        {
            IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByArtistListQuery() { ArtistIds = artistIds });

            if (artistIds.Count == 1)
                tracks = TracksRandomizer.Randomize(tracks);
            else
                tracks = ArtistBalancedTrackRandomizer.Randomize(tracks);

            _playerService.LoadPlaylist(tracks.ToList());
        }
        else
            _logger.LogDebug("No track to listen.");
    }


    private async Task ListenGroupAsync(ArtistsGroupCategoryViewModel group)
    {
        List<long> artistIds = [];
        artistIds.AddRange(group.Items.Select(artist => artist.Artist.Id));

        await ListenAsync(artistIds);
    }


    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Selected.CollectionChanged -= Selected_CollectionChanged;
                ViewModels.Clear();
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