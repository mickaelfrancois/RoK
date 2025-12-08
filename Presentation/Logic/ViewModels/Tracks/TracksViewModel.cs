using Rok.Logic.ViewModels.Albums.Handlers;
using Rok.Logic.ViewModels.Tracks.Handlers;
using Rok.Logic.ViewModels.Tracks.Services;

namespace Rok.Logic.ViewModels.Tracks;

public partial class TracksViewModel : ObservableObject, IDisposable
{
    private readonly TracksGroupCategory _groupService;
    private readonly TracksFilter _filterService;
    private readonly ILogger<TracksViewModel> _logger;

    private readonly TracksDataLoader _dataLoader;
    private readonly TracksSelectionManager _selectionManager;
    private readonly TracksStateManager _stateManager;
    private readonly TracksPlaybackService _playbackService;

    private readonly LibraryRefreshMessageHandler _libraryRefreshHandler;
    private readonly TrackImportedMessageHandler _trackImportedHandler;

    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<TrackViewModel> _filteredTracks = [];

    public List<TrackViewModel> ViewModels => _dataLoader.ViewModels;
    public List<GenreDto> Genres => _dataLoader.Genres;
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public List<TrackViewModel> SelectedItems => _selectionManager.SelectedItems;
    public int SelectedCount => _selectionManager.SelectedCount;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public RangeObservableCollection<TracksGroupCategoryViewModel> GroupedItems { get; private set; } = [];

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
    public RelayCommand ListenCommand { get; private set; }
    public RelayCommand<TracksGroupCategoryViewModel> ListenGroupCommand { get; private set; }

    public TracksViewModel(
        TracksFilter tracksFilter,
        TracksGroupCategory tracksGroupCategory,
        TracksDataLoader dataLoader,
        TracksSelectionManager selectionManager,
        TracksStateManager stateManager,
        TracksPlaybackService playbackService,
        LibraryRefreshMessageHandler libraryRefreshHandler,
        TrackImportedMessageHandler trackImportedHandler,
        ILogger<TracksViewModel> logger)
    {
        _groupService = tracksGroupCategory;
        _filterService = tracksFilter;
        _dataLoader = dataLoader;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _libraryRefreshHandler = libraryRefreshHandler;
        _trackImportedHandler = trackImportedHandler;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenCommand = new RelayCommand(ListenTracks);
        ListenGroupCommand = new RelayCommand<TracksGroupCategoryViewModel>(ListenGroup);

        SubscribeToMessages();
        SubscribeToEvents();
    }


    private void SubscribeToMessages()
    {
        Messenger.Subscribe<LibraryRefreshMessage>(_libraryRefreshHandler.Handle);
        Messenger.Subscribe<AlbumImportedMessage>(_trackImportedHandler.Handle);
    }

    private void SubscribeToEvents()
    {
        _selectionManager.SelectionChanged += OnSelectionChanged;
        _libraryRefreshHandler.LibraryChanged += OnLibraryChanged;
        _trackImportedHandler.TrackImported += OnTrackImported;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(TotalCount));
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    private void OnTrackImported(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Tracks already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;
        _libraryRefreshHandler.ResetLibraryUpdatedFlag();
        _trackImportedHandler.ResetLibraryUpdatedFlag();

        if (!_stateLoaded)
            LoadState();

        await _dataLoader.LoadGenresAsync();
        await _dataLoader.LoadTracksAsync();

        FilterAndSort();
    }

    public void SetData(List<TrackDto> tracks)
    {
        _dataLoader.SetTracks(tracks);
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
        IEnumerable<TrackViewModel> filteredTracks = ViewModels;

        foreach (string filterby in _stateManager.SelectedFilters)
            filteredTracks = _filterService.Filter(filterby, filteredTracks);

        foreach (long genreId in _stateManager.SelectedGenreFilters)
            filteredTracks = _filterService.FilterByGenreId(genreId, filteredTracks);

        _filteredTracks = filteredTracks.ToList();

        IEnumerable<TracksGroupCategoryViewModel> tracks = _groupService.GetGroupedItems(_stateManager.GroupBy, _filteredTracks);
        GroupedItems.InitWithAddRange(tracks);

        TotalCount = _filteredTracks.Count;
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

    private void ListenGroup(TracksGroupCategoryViewModel group)
    {
        List<TrackDto> tracks = group.Items.Select(track => track.Track).ToList();
        _playbackService.PlayTracks(tracks);
    }

    private void ListenTracks()
    {
        List<TrackDto> tracks = Selected.Count == 0
            ? _filteredTracks.Select(track => track.Track).ToList()
            : SelectedItems.Select(track => track.Track).ToList();

        _playbackService.PlayTracks(tracks);
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
                _libraryRefreshHandler.LibraryChanged -= OnLibraryChanged;
                _trackImportedHandler.TrackImported -= OnTrackImported;
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