using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Artists.Services;

namespace Rok.ViewModels.Artists;

public partial class ArtistsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ArtistsViewModel> _logger;
    private readonly IArtistProvider _artistProvider;
    private readonly IArtistLibraryMonitor _libraryMonitor;
    private readonly TagsProvider _tagsProvider;
    private readonly IAppOptions _appOptions;
    private readonly ArtistsSelectionManager _selectionManager;
    private readonly ArtistsStateManager _stateManager;
    private readonly ArtistsPlaybackService _playbackService;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<ArtistViewModel> _filteredArtists = [];

    public RangeObservableCollection<ArtistsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    public IReadOnlyList<ArtistViewModel> ViewModels => _artistProvider.ViewModels;
    public IReadOnlyList<GenreDto> Genres => _artistProvider.Genres;
    public IReadOnlyList<string> Tags { get; private set; } = [];
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public IReadOnlyList<ArtistViewModel> SelectedItems => _selectionManager.SelectedItems;
    public IReadOnlyList<string> SelectedFilters => _stateManager.SelectedFilters;
    public IReadOnlyList<long> SelectedGenreFilters => _stateManager.SelectedGenreFilters;
    public IReadOnlyList<string> SelectedTagFilters => _stateManager.SelectedTagFilters;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public int Count => _filteredArtists.Count;
    public bool HasNoData => _filteredArtists.Count == 0;
    public double DurationText => TimeSpan.FromSeconds(_filteredArtists.Sum(artist => artist.Artist.TotalDurationSeconds)).TotalHours;

    [ObservableProperty]
    public partial string FilterByText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsGroupingEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsGridView { get; set; }
    partial void OnIsGridViewChanged(bool value)
    {
        _stateManager.SaveGridView(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupById))]
    [NotifyPropertyChangedFor(nameof(GroupByText))]
    public partial string SelectedGroupBy { get; set; } = string.Empty;
    partial void OnSelectedGroupByChanged(string value)
    {
        _stateManager.GroupBy = value;
    }
    public string GroupById => SelectedGroupBy;
    public string GroupByText => _artistProvider.GetGroupByLabel(SelectedGroupBy);


    public ArtistsViewModel(TagsProvider tagProvider, IArtistProvider artistProvider, IArtistLibraryMonitor libraryMonitor, ArtistsSelectionManager selectionManager, ArtistsStateManager stateManager, ArtistsPlaybackService playbackService, IAppOptions appOptions, ILogger<ArtistsViewModel> logger)
    {
        _tagsProvider = tagProvider;
        _artistProvider = artistProvider;
        _libraryMonitor = libraryMonitor;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _appOptions = appOptions;
        _logger = logger;

        IsGridView = _stateManager.GetGridView();
        _libraryMonitor.LibraryChanged += OnLibraryChanged;
    }


    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        _dispatcherQueue.TryEnqueue(() => FilterAndSort());
    }


    public async Task LoadDataAsync(bool forceReload)
    {
        IsGridView = _stateManager.GetGridView();

        Tags = await _tagsProvider.GetTagsAsync();

        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Artists already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;
        _libraryMonitor.ResetUpdateFlags();

        if (!_stateLoaded)
            LoadState();

        await _artistProvider.LoadAsync(_appOptions.HideArtistsWithoutAlbum);

        FilterAndSort();
    }

    public void SetData(List<ArtistDto> artists)
    {
        _artistProvider.SetArtists(artists);
        FilterAndSort();
    }

    private void SetFilterLabel()
    {
        if (_stateManager.SelectedFilters.Count > 0)
        {
            string lastFilter = _stateManager.SelectedFilters[^1];
            FilterByText = _artistProvider.GetFilterLabel(lastFilter);
        }
        else if (_stateManager.SelectedGenreFilters.Count > 0)
        {
            long lastGenreId = _stateManager.SelectedGenreFilters[^1];
            FilterByText = Genres.FirstOrDefault(c => c.Id == lastGenreId)?.Name ?? "";
        }
        else
        {
            FilterByText = _artistProvider.GetFilterLabel("");
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
    private void ToggleDisplayMode()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    private void FilterBy(string filterBy)
    {
        if (string.IsNullOrEmpty(filterBy))
        {
            _stateManager.SelectedFilters.Clear();
            _stateManager.SelectedGenreFilters.Clear();
            _stateManager.SelectedTagFilters.Clear();
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
    private void FilterByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            _stateManager.SelectedTagFilters.Clear();
        else if (_stateManager.SelectedTagFilters.Contains(tag))
            _stateManager.SelectedTagFilters.Remove(tag);
        else
            _stateManager.SelectedTagFilters.Add(tag);

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
        ArtistProviderResult result = _artistProvider.GetProcessedData(_stateManager.GroupBy, _stateManager.SelectedFilters, _stateManager.SelectedGenreFilters, _stateManager.SelectedTagFilters);

        _filteredArtists = result.FilteredItems;

        ApplyNewBadge();

        GroupedItems.InitWithAddRange(result.Groups);
        IsGroupingEnabled = result.IsGroupingEnabled;

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(HasNoData));
    }

    private void ApplyNewBadge()
    {
        bool allNew = _filteredArtists.Count > 0 && _filteredArtists.All(a => a.IsNew);

        foreach (ArtistViewModel viewModel in _filteredArtists)
            viewModel.ShowNewBadge = viewModel.IsNew && !allNew;
    }

    [RelayCommand]
    private async Task ListenGroupAsync(ArtistsGroupCategoryViewModel group)
    {
        List<long> artistIds = group.Items.Select(artist => artist.Artist.Id).ToList();
        await _playbackService.PlayArtistsAsync(artistIds);
    }

    [RelayCommand]
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
                _libraryMonitor.LibraryChanged -= OnLibraryChanged;
                _artistProvider.Clear();
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