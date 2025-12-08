using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Artists.Handlers;
using Rok.Logic.ViewModels.Artists.Services;

namespace Rok.Logic.ViewModels.Artists;

public partial class ArtistsViewModel : ObservableObject, IDisposable
{
    private readonly ArtistsGroupCategory _groupService;
    private readonly ArtistsFilter _filterService;
    private readonly IAppOptions _appOptions;
    private readonly ILogger<ArtistsViewModel> _logger;

    private readonly ArtistsDataLoader _dataLoader;
    private readonly ArtistsSelectionManager _selectionManager;
    private readonly ArtistsStateManager _stateManager;
    private readonly ArtistsPlaybackService _playbackService;

    private readonly ArtistUpdateMessageHandler _artistUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly ArtistImportedMessageHandler _artistImportedHandler;

    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<ArtistViewModel> _filteredArtists = [];

    public List<ArtistViewModel> ViewModels => _dataLoader.ViewModels;
    public List<GenreDto> Genres => _dataLoader.Genres;
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public List<ArtistViewModel> SelectedItems => _selectionManager.SelectedItems;
    public int SelectedCount => _selectionManager.SelectedCount;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public RangeObservableCollection<ArtistsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    private int _totalCount = 0;
    public int TotalCount
    {
        get => Selected.Count > 0 ? Selected.Count : _totalCount;
        set => SetProperty(ref _totalCount, value);
    }

    public string GroupById => _stateManager.GroupBy;
    public string GroupByText
    {
        get => _groupService.GetGroupByLabel(_stateManager.GroupBy);
        set
        {
            _stateManager.GroupBy = value;
            OnPropertyChanged();
        }
    }

    public List<string> SelectedFilters => _stateManager.SelectedFilters;
    public List<long> SelectedGenreFilters => _stateManager.SelectedGenreFilters;

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

    public ArtistsViewModel(
        ArtistsFilter artistsFilter,
        ArtistsGroupCategory artistsGroupCategory,
        ArtistsDataLoader dataLoader,
        ArtistsSelectionManager selectionManager,
        ArtistsStateManager stateManager,
        ArtistsPlaybackService playbackService,
        ArtistUpdateMessageHandler artistUpdateHandler,
        LibraryRefreshMessageHandler libraryRefreshHandler,
        ArtistImportedMessageHandler artistImportedHandler,
        IAppOptions appOptions,
        ILogger<ArtistsViewModel> logger)
    {
        _groupService = artistsGroupCategory;
        _filterService = artistsFilter;
        _dataLoader = dataLoader;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _artistUpdateHandler = artistUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _artistImportedHandler = artistImportedHandler;
        _appOptions = appOptions;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenGroupCommand = new AsyncRelayCommand<ArtistsGroupCategoryViewModel>(ListenGroupAsync);
        ListenCommand = new AsyncRelayCommand(ListenAsync);

        SubscribeToMessages();
        SubscribeToEvents();
    }


    private void SubscribeToMessages()
    {
        Messenger.Subscribe<ArtistUpdateMessage>(async (message) => await _artistUpdateHandler.HandleAsync(message));
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<ArtistImportedMessage>(_artistImportedHandler.Handle);
    }

    private void SubscribeToEvents()
    {
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _artistUpdateHandler.DataChanged += OnDataChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _artistImportedHandler.ArtistImported += OnArtistImported;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalCount));
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        FilterAndSort();
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    private void OnArtistImported(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Artists already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _artistImportedHandler.ResetLibraryUpdatedFlag();

        if (!_stateLoaded)
            LoadState();

        await _dataLoader.LoadGenresAsync();
        await _dataLoader.LoadArtistsAsync(_appOptions.HideArtistsWithoutAlbum);

        FilterAndSort();
    }

    public void SetData(List<ArtistDto> artists)
    {
        _dataLoader.SetArtists(artists);
        FilterAndSort();
    }

    private void FilterBy(string filterBy)
    {
        if (string.IsNullOrEmpty(filterBy))
        {
            _stateManager.SelectedFilters.Clear();
            _stateManager.SelectedGenreFilters.Clear();
        }
        else if (_stateManager.SelectedFilters.Contains(filterBy))
            _stateManager.SelectedFilters.Remove(filterBy);
        else
            _stateManager.SelectedFilters.Add(filterBy);

        SetFilterLabel();
        FilterAndSort();
    }

    private void FilterByGenreId(long? id)
    {
        if (id == null)
            _stateManager.SelectedGenreFilters.Clear();
        else if (_stateManager.SelectedGenreFilters.Contains(id.Value))
            _stateManager.SelectedGenreFilters.Remove(id.Value);
        else
            _stateManager.SelectedGenreFilters.Add(id.Value);

        SetFilterLabel();
        FilterAndSort();
    }

    private void SetFilterLabel()
    {
        if (_stateManager.SelectedFilters.Count > 0)
            FilterByText = _filterService.GetLabel(_stateManager.SelectedFilters[_stateManager.SelectedFilters.Count - 1]);
        else if (_stateManager.SelectedGenreFilters.Count > 0)
            FilterByText = Genres.FirstOrDefault(c => c.Id == _stateManager.SelectedGenreFilters[_stateManager.SelectedGenreFilters.Count - 1])?.Name ?? "";
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

        foreach (string filterby in _stateManager.SelectedFilters)
            filteredArtists = _filterService.Filter(filterby, filteredArtists);

        foreach (long genreId in _stateManager.SelectedGenreFilters)
            filteredArtists = _filterService.FilterByGenreId(genreId, filteredArtists);

        _filteredArtists = filteredArtists.ToList();

        IEnumerable<ArtistsGroupCategoryViewModel> artists = _groupService.GetGroupedItems(_stateManager.GroupBy, _filteredArtists);
        GroupedItems.InitWithAddRange(artists);

        TotalCount = _filteredArtists.Count;
    }

    public void SaveState()
    {
        _stateManager.Save();
    }

    private void LoadState()
    {
        _stateLoaded = true;
        _stateManager.Load();
        SetFilterLabel();

        OnPropertyChanged(nameof(GroupByText));
        OnPropertyChanged(nameof(FilterByText));
    }

    private async Task ListenGroupAsync(ArtistsGroupCategoryViewModel group)
    {
        List<long> artistIds = group.Items.Select(artist => artist.Artist.Id).ToList();
        await _playbackService.PlayArtistsAsync(artistIds);
    }

    private async Task ListenAsync()
    {
        List<long> artistIds = Selected.Count == 0
            ? _filteredArtists.Select(artist => artist.Artist.Id).ToList()
            : SelectedItems.Select(artist => artist.Artist.Id).ToList();

        await _playbackService.PlayArtistsAsync(artistIds);
    }

    #region IDisposable Support

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _selectionManager.SelectionChanged -= OnSelectionChanged;
                _artistUpdateHandler.DataChanged -= OnDataChanged;
                _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
                _artistImportedHandler.ArtistImported -= OnArtistImported;
                _dataLoader.Clear();
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