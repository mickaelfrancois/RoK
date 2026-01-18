using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Albums.Services;

namespace Rok.Logic.ViewModels.Albums;

public partial class AlbumsViewModel : ObservableObject, IDisposable
{
    private readonly AlbumsGroupCategory _groupService;
    private readonly AlbumsFilter _filterService;
    private readonly ILogger<AlbumsViewModel> _logger;

    private readonly AlbumsDataLoader _dataLoader;
    private readonly AlbumsSelectionManager _selectionManager;
    private readonly AlbumsStateManager _stateManager;
    private readonly AlbumsPlaybackService _playbackService;

    private readonly AlbumUpdateMessageHandler _albumUpdateHandler;
    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly AlbumImportedMessageHandler _albumImportedHandler;

    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<AlbumViewModel> _filteredAlbums = [];

    private bool _isGroupingEnabled;
    public bool IsGroupingEnabled
    {
        get => _isGroupingEnabled;
        set
        {
            _isGroupingEnabled = value;
            OnPropertyChanged(nameof(IsGroupingEnabled));
        }
    }

    public List<AlbumViewModel> ViewModels => _dataLoader.ViewModels;
    public List<GenreDto> Genres => _dataLoader.Genres;
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public List<AlbumViewModel> SelectedItems => _selectionManager.SelectedItems;
    public int SelectedCount => _selectionManager.SelectedCount;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public RangeObservableCollection<AlbumsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

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

    private bool _isGridView = true;
    public bool IsGridView
    {
        get
        {
            return _isGridView;
        }
        private set
        {
            _isGridView = value;
            OnPropertyChanged(nameof(IsGridView));
        }
    }

    public RelayCommand<long?> FilterByGenreCommand { get; private set; }
    public RelayCommand<string> FilterByCommand { get; private set; }
    public RelayCommand<string> GroupByCommand { get; private set; }
    public AsyncRelayCommand ListenCommand { get; private set; }
    public AsyncRelayCommand<AlbumsGroupCategoryViewModel> ListenGroupCommand { get; private set; }
    public RelayCommand ToggleDisplayModeCommand { get; private set; }

    public AlbumsViewModel(
        AlbumsFilter albumsFilter,
        AlbumsGroupCategory albumsGroupCategory,
        AlbumsDataLoader dataLoader,
        AlbumsSelectionManager selectionManager,
        AlbumsStateManager stateManager,
        AlbumsPlaybackService playbackService,
        AlbumUpdateMessageHandler albumUpdateHandler,
        LibraryRefreshMessageHandler libraryRefreshHandler,
        AlbumImportedMessageHandler albumImportedHandler,
        ILogger<AlbumsViewModel> logger)
    {
        _groupService = albumsGroupCategory;
        _filterService = albumsFilter;
        _dataLoader = dataLoader;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _albumUpdateHandler = albumUpdateHandler;
        _libraryRefreshHandler = libraryRefreshHandler;
        _albumImportedHandler = albumImportedHandler;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenGroupCommand = new AsyncRelayCommand<AlbumsGroupCategoryViewModel>(ListenGroupAsync);
        ListenCommand = new AsyncRelayCommand(ListenAsync);
        ToggleDisplayModeCommand = new RelayCommand(() => IsGridView = !IsGridView);

        SubscribeToMessages();
        SubscribeToEvents();
    }

    private void SubscribeToMessages()
    {
        Messenger.Subscribe<AlbumUpdateMessage>(async (message) => await _albumUpdateHandler.HandleAsync(message));
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<AlbumImportedMessage>(_albumImportedHandler.Handle);
    }

    private void SubscribeToEvents()
    {
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _albumUpdateHandler.DataChanged += OnDataChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _albumImportedHandler.AlbumImported += OnAlbumImported;
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

    private void OnAlbumImported(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Albums already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _albumImportedHandler.ResetLibraryUpdatedFlag();

        if (!_stateLoaded)
            LoadState();

        await _dataLoader.LoadGenresAsync();
        await _dataLoader.LoadAlbumsAsync();

        FilterAndSort();
    }

    public void SetData(List<AlbumDto> albums)
    {
        _dataLoader.SetAlbums(albums);
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
        IEnumerable<AlbumViewModel> filteredAlbums = ViewModels;

        foreach (string filterby in _stateManager.SelectedFilters)
            filteredAlbums = _filterService.Filter(filterby, filteredAlbums);

        foreach (long genreId in _stateManager.SelectedGenreFilters)
            filteredAlbums = _filterService.FilterByGenreId(genreId, filteredAlbums);

        _filteredAlbums = filteredAlbums.ToList();

        IEnumerable<AlbumsGroupCategoryViewModel> albums = _groupService.GetGroupedItems(_stateManager.GroupBy, _filteredAlbums);
        GroupedItems.InitWithAddRange(albums);

        IsGroupingEnabled = GroupedItems.Count > 1 || !string.IsNullOrEmpty(GroupedItems.FirstOrDefault()?.Title ?? string.Empty);

        TotalCount = _filteredAlbums.Count;
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

    private async Task ListenGroupAsync(AlbumsGroupCategoryViewModel group)
    {
        List<long> albumIds = group.Items.Select(album => album.Album.Id).ToList();
        await _playbackService.PlayAlbumsAsync(albumIds);
    }

    private async Task ListenAsync()
    {
        List<long> albumIds = Selected.Count == 0
            ? _filteredAlbums.Select(album => album.Album.Id).ToList()
            : SelectedItems.Select(album => album.Album.Id).ToList();

        await _playbackService.PlayAlbumsAsync(albumIds);
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
                _albumUpdateHandler.DataChanged -= OnDataChanged;
                _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
                _albumImportedHandler.AlbumImported -= OnAlbumImported;
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