using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Tracks;

public partial class TracksViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly TracksGroupCategory _groupService;
    private readonly TracksFilter _filterService;
    private readonly ILogger<TracksViewModel> _logger;
    private readonly IPlayerService _playerService;
    private readonly IAppOptions _appOptions;

    public List<TrackViewModel> ViewModels { get; set; } = [];

    private bool _libraryUpdated = false;
    private readonly bool _stateLoaded = false;

    public RangeObservableCollection<TracksGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    private List<TrackViewModel> _filteredTracks = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public ObservableCollection<object> Selected { get; } = [];

    public List<TrackViewModel> SelectedItems
    {
        get
        {
            List<TrackViewModel> list = [];

            if (Selected.Count > 0)
            {
                list.AddRange(Selected.Select(c => (TrackViewModel)c));
            }

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

    private string _groupByText = TracksGroupCategory.KGroupByTitle;
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
    public RelayCommand ListenCommand { get; private set; }
    public RelayCommand<TracksGroupCategoryViewModel> ListenGroupCommand { get; private set; }


    public TracksViewModel(IMediator mediator, IPlayerService playerService, TracksFilter tracksFilter, TracksGroupCategory tracksGroupCategory, IAppOptions appOptions, ILogger<TracksViewModel> logger)
    {
        _mediator = mediator;
        _playerService = playerService;
        _groupService = tracksGroupCategory;
        _filterService = tracksFilter;
        _appOptions = appOptions;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenCommand = new RelayCommand(ListenTracks);
        ListenGroupCommand = new RelayCommand<TracksGroupCategoryViewModel>(ListenGroup);

        Messenger.Subscribe<LibraryRefreshMessage>(LibraryRefreshHandle);

        Selected.CollectionChanged += Selected_CollectionChanged;
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
        if (!mustLoad)
        {
            _logger.LogInformation("tracks already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;

        if (!_stateLoaded)
            LoadState();

        await LoadGenresAsync();

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Tracks loaded"))
        {
            IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetAllTracksQuery());
            ViewModels = TrackViewModelMap.CreateViewModels(tracks);

            FilterAndSort();
        }
    }


    public void SetData(List<TrackDto> tracks)
    {
        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Tracks loaded"))
        {
            ViewModels = TrackViewModelMap.CreateViewModels(tracks);

            FilterAndSort();
        }
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
        IEnumerable<TrackViewModel> filteredTracks = ViewModels;
        foreach (string filterby in _selectedFilters)
            filteredTracks = TracksFilter.Filter(filterby, filteredTracks);

        foreach (long genreId in _selectedGenreFilters)
            filteredTracks = TracksFilter.FilterByGenreId(genreId, filteredTracks);

        _filteredTracks = filteredTracks.ToList();

        IEnumerable<TracksGroupCategoryViewModel> tracks = TracksGroupCategory.GetGroupedItems(_groupByText, _filteredTracks);
        GroupedItems.InitWithAddRange(tracks);

        TotalCount = _filteredTracks.Count;
    }


    private void ListenGroup(TracksGroupCategoryViewModel group)
    {
        List<TrackDto> tracks = new();
        tracks.AddRange(group.Items.Select(track => track.Track));

        ListenTracks(tracks);
    }


    private void ListenTracks()
    {
        List<TrackDto> tracks = new();

        if (Selected.Count == 0)
            tracks.AddRange(_filteredTracks.Select(track => track.Track));
        else
            tracks.AddRange(SelectedItems.Select(track => track.Track));

        ListenTracks(tracks);
    }


    private void ListenTracks(List<TrackDto> tracks)
    {
        if (tracks.Count != 0)
        {
            if (tracks.Count > 1)
                tracks = TracksRandomizer.Randomize(tracks);

            _playerService.LoadPlaylist(tracks);
        }
        else
            _logger.LogDebug("No track to listen.");
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
        _appOptions.TracksGroupBy = _groupByText;
        _appOptions.TracksFilterBy = _selectedFilters;
        _appOptions.TracksFilterByGenresId = _selectedGenreFilters;
    }


    private void LoadState()
    {
        _groupByText = string.IsNullOrEmpty(_appOptions.TracksGroupBy) ? TracksGroupCategory.KGroupByAlbum : _appOptions.TracksGroupBy;
        _selectedFilters = _appOptions.TracksFilterBy;
        _selectedGenreFilters = _appOptions.TracksFilterByGenresId;
        SetFilterLabel();

        OnPropertyChanged(nameof(GroupByText));
        OnPropertyChanged(nameof(FilterByText));
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