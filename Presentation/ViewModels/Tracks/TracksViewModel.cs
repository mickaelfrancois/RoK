using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.ViewModels.Track;
using Rok.ViewModels.Tracks.Interfaces;
using Rok.ViewModels.Tracks.Services;

namespace Rok.ViewModels.Tracks;

public partial class TracksViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<TracksViewModel> _logger;
    private readonly ITrackProvider _trackProvider;
    private readonly ITrackLibraryMonitor _libraryMonitor;
    private readonly TracksSelectionManager _selectionManager;
    private readonly TracksStateManager _stateManager;
    private readonly TracksPlaybackService _playbackService;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<TrackViewModel> _filteredTracks = [];

    public RangeObservableCollection<TracksGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    public IReadOnlyList<TrackViewModel> ViewModels => _trackProvider.ViewModels;
    public IReadOnlyList<GenreDto> Genres => _trackProvider.Genres;
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public IReadOnlyList<TrackViewModel> SelectedItems => _selectionManager.SelectedItems;
    public IReadOnlyList<string> SelectedFilters => _stateManager.SelectedFilters;
    public IReadOnlyList<long> SelectedGenreFilters => _stateManager.SelectedGenreFilters;
    public int SelectedCount => _selectionManager.SelectedCount;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public int Count => _filteredTracks.Count;
    public double DurationText => TimeSpan.FromSeconds(_filteredTracks.Sum(track => track.Track.Duration)).TotalHours;

    [ObservableProperty]
    public partial string FilterByText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGroupingEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupById))]
    [NotifyPropertyChangedFor(nameof(GroupByText))]
    public partial string SelectedGroupBy { get; set; } = string.Empty;
    partial void OnSelectedGroupByChanged(string value)
    {
        _stateManager.GroupBy = value;
    }
    public string GroupById => SelectedGroupBy;
    public string GroupByText => _trackProvider.GetGroupByLabel(SelectedGroupBy);


    public TracksViewModel(ITrackProvider trackProvider, ITrackLibraryMonitor libraryMonitor, TracksSelectionManager selectionManager, TracksStateManager stateManager, TracksPlaybackService playbackService, ILogger<TracksViewModel> logger)
    {
        _trackProvider = trackProvider;
        _libraryMonitor = libraryMonitor;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _logger = logger;

        _libraryMonitor.LibraryChanged += OnLibraryChanged;
    }


    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        _dispatcherQueue.TryEnqueue(() => FilterAndSort());
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
        _libraryMonitor.ResetUpdateFlags();

        if (!_stateLoaded)
            LoadState();

        await _trackProvider.LoadAsync();

        FilterAndSort();
    }

    public void SetData(List<TrackDto> tracks)
    {
        _trackProvider.SetTracks(tracks);
        FilterAndSort();
    }

    private void SetFilterLabel()
    {
        if (_stateManager.SelectedFilters.Count > 0)
        {
            string lastFilter = _stateManager.SelectedFilters[^1];
            FilterByText = _trackProvider.GetFilterLabel(lastFilter);
        }
        else if (_stateManager.SelectedGenreFilters.Count > 0)
        {
            long lastGenreId = _stateManager.SelectedGenreFilters[^1];
            FilterByText = Genres.FirstOrDefault(c => c.Id == lastGenreId)?.Name ?? "";
        }
        else
        {
            FilterByText = _trackProvider.GetFilterLabel("");
        }
    }

    private void LoadState()
    {
        _stateLoaded = true;
        _stateManager.Load();
        SelectedGroupBy = _stateManager.GroupBy;
        SetFilterLabel();
    }

    public void SaveState()
    {
        _stateManager.Save();
    }


    [RelayCommand]
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

    [RelayCommand]
    private void FilterByGenre(long? id)
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

    [RelayCommand]
    private void GroupBy(string groupBy)
    {
        SelectedGroupBy = groupBy;
        FilterAndSort();
    }

    [RelayCommand]
    private void FilterAndSort()
    {
        TrackProviderResult result = _trackProvider.GetProcessedData(_stateManager.GroupBy, _stateManager.SelectedFilters, _stateManager.SelectedGenreFilters);

        _filteredTracks = result.FilteredItems;
        GroupedItems.InitWithAddRange(result.Groups);
        IsGroupingEnabled = result.IsGroupingEnabled;

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(DurationText));
    }

    [RelayCommand]
    private void ListenGroup(TracksGroupCategoryViewModel group)
    {
        List<TrackDto> tracks = group.Items.Select(track => track.Track).ToList();
        _playbackService.PlayTracks(tracks);
    }

    [RelayCommand]
    private void Listen()
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
                _libraryMonitor.LibraryChanged -= OnLibraryChanged;
                _trackProvider.Clear();
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