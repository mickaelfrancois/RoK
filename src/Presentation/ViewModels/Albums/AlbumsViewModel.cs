using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Albums.Services;
using Rok.ViewModels.Listening.Services;

namespace Rok.ViewModels.Albums;

public partial class AlbumsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<AlbumsViewModel> _logger;
    private readonly IAlbumProvider _albumProvider;
    private readonly IAlbumLibraryMonitor _libraryMonitor;
    private readonly TagsProvider _tagsProvider;
    private readonly AlbumsSelectionManager _selectionManager;
    private readonly AlbumsStateManager _stateManager;
    private readonly AlbumsPlaybackService _playbackService;
    private readonly ITelemetryClient _telemetryClient;
    private readonly IStringResourceProvider _resourceLoader;
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<AlbumViewModel> _filteredAlbums = [];

    public RangeObservableCollection<AlbumsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    public IReadOnlyList<AlbumViewModel> ViewModels => _albumProvider.ViewModels;
    public IReadOnlyList<GenreDto> Genres => _albumProvider.Genres;
    public IReadOnlyList<string> Tags { get; private set; } = [];
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public IReadOnlyList<AlbumViewModel> SelectedItems => _selectionManager.SelectedItems;
    public IReadOnlyList<string> SelectedFilters => _stateManager.SelectedFilters;
    public IReadOnlyList<long> SelectedGenreFilters => _stateManager.SelectedGenreFilters;
    public IReadOnlyList<string> SelectedTagFilters => _stateManager.SelectedTagFilters;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

    public int Count => _filteredAlbums.Count;
    public bool HasNoData => _filteredAlbums.Count == 0;
    public double DurationText => TimeSpan.FromSeconds(_filteredAlbums.Sum(album => album.Album.Duration)).TotalHours;

    /// <summary>Album count and total duration of the filtered list, with the localized formats of the other headers.</summary>
    public string HeaderSummary
    {
        get
        {
            ListeningSummaryLabels labels = new(
                _resourceLoader.GetString("artistSummaryAlbum"),
                _resourceLoader.GetString("artistSummaryAlbums"),
                _resourceLoader.GetString("listeningSummaryHoursMinutes"),
                _resourceLoader.GetString("listeningSummaryHours"),
                _resourceLoader.GetString("listeningSummaryMinutes"),
                _resourceLoader.GetString("listeningSummaryEndsAt"));

            long totalSeconds = _filteredAlbums.Sum(album => (long)album.Album.Duration);

            return ListeningQueueSummaryFormatter.FormatOverview(new ListeningQueueSummary(_filteredAlbums.Count, totalSeconds, null), labels, CultureInfo.CurrentCulture);
        }
    }

    /// <summary>One chip per active filter, whichever family (list, genre, tag) it comes from.</summary>
    public ObservableCollection<AlbumFilterChip> ActiveFilters { get; } = [];
    public bool HasActiveFilters => ActiveFilters.Count > 0;
    public bool HasSeveralFilters => ActiveFilters.Count > 1;

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
    public partial string SearchText { get; set; }
    partial void OnSearchTextChanged(string value) => FilterAndSort();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupById))]
    [NotifyPropertyChangedFor(nameof(GroupByText))]
    public partial string SelectedGroupBy { get; set; } = string.Empty;
    partial void OnSelectedGroupByChanged(string value)
    {
        _stateManager.GroupBy = value;
    }
    public string GroupById => SelectedGroupBy;
    public string GroupByText => _albumProvider.GetGroupByLabel(SelectedGroupBy);

    public AlbumsViewModel(TagsProvider tagProvider, IAlbumProvider albumProvider, IAlbumLibraryMonitor libraryMonitor, AlbumsSelectionManager selectionManager, AlbumsStateManager stateManager, AlbumsPlaybackService playbackService, ITelemetryClient telemetryClient, IStringResourceProvider resourceLoader, ILogger<AlbumsViewModel> logger)
    {
        _tagsProvider = tagProvider;
        _albumProvider = albumProvider;
        _libraryMonitor = libraryMonitor;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _telemetryClient = telemetryClient;
        _resourceLoader = resourceLoader;
        _logger = logger;

        IsGridView = _stateManager.GetGridView();
        _libraryMonitor.LibraryChanged += OnLibraryChanged;
        _libraryMonitor.LibraryRefreshed += OnLibraryRefreshed;
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        _dispatcherQueue.TryEnqueue(() => FilterAndSort());
    }

    private void OnLibraryRefreshed(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        _dispatcherQueue.TryEnqueue(async () => await ReloadAfterScanAsync());
    }

    private async Task ReloadAfterScanAsync()
    {
        if (ViewModels.Count == 0)
            return;

        try
        {
            await LoadDataAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload albums after the library scan.");
        }
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        IsGridView = _stateManager.GetGridView();

        Tags = await _tagsProvider.GetTagsAsync();

        bool mustLoad = _libraryUpdated || forceReload || ViewModels.Count == 0;
        if (!mustLoad)
        {
            _logger.LogInformation("Albums already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;
        _libraryMonitor.ResetUpdateFlags();

        if (!_stateLoaded)
            LoadState();

        await _albumProvider.LoadAsync();

        _stateManager.PruneGenreFilters(Genres.Select(g => g.Id));
        _stateManager.PruneTagFilters(Tags);
        SetFilterLabel();

        FilterAndSort();
    }

    public void SetData(List<AlbumDto> albums)
    {
        _albumProvider.SetAlbums(albums);
        FilterAndSort();
    }

    private void SetFilterLabel()
    {
        ActiveFilters.Clear();

        foreach (AlbumFilterChip chip in AlbumFilterChipBuilder.Build(
                     _stateManager.SelectedFilters, _stateManager.SelectedGenreFilters, _stateManager.SelectedTagFilters, Genres, _albumProvider.GetFilterLabel))
        {
            ActiveFilters.Add(chip);
        }

        FilterByText = ActiveFilters.Count > 0
            ? ActiveFilters.Count.ToString(CultureInfo.CurrentCulture)
            : _albumProvider.GetFilterLabel("");

        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasSeveralFilters));
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
    private void RemoveFilter(AlbumFilterChip chip)
    {
        switch (chip.Kind)
        {
            case AlbumFilterKind.List:
                FilterBy(chip.Value);
                break;
            case AlbumFilterKind.Genre:
                FilterByGenre(long.Parse(chip.Value, CultureInfo.InvariantCulture));
                break;
            case AlbumFilterKind.Tag:
                FilterByTag(chip.Value);
                break;
        }
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
        AlbumProviderResult result = _albumProvider.GetProcessedData(_stateManager.GroupBy, _stateManager.SelectedFilters, _stateManager.SelectedGenreFilters, _stateManager.SelectedTagFilters);

        _filteredAlbums = result.FilteredItems;
        var groups = result.Groups.ToList();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _filteredAlbums = _filteredAlbums
                .Where(a => a.Album.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                            a.Album.ArtistName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            groups = groups
                .Select(g => new AlbumsGroupCategoryViewModel
                {
                    Title = g.Title,
                    Items = g.Items
                        .Where(a => a.Album.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    a.Album.ArtistName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList()
                })
                .Where(g => g.Items.Count > 0)
                .ToList();
        }

        ApplyNewBadge();

        // Publish the grouping flag before the data: the page refreshes on the collection Reset,
        // which must therefore be the last notification of the pass.
        IsGroupingEnabled = groups.Count > 1 || !string.IsNullOrEmpty(groups.FirstOrDefault()?.Title ?? string.Empty);
        GroupedItems.InitWithAddRange(groups);

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(HeaderSummary));
        OnPropertyChanged(nameof(HasNoData));
    }

    private void ApplyNewBadge()
    {
        bool allNew = _filteredAlbums.Count > 0 && _filteredAlbums.All(a => a.IsNew);

        foreach (AlbumViewModel viewModel in _filteredAlbums)
            viewModel.ShowNewBadge = viewModel.IsNew && !allNew;
    }

    [RelayCommand]
    private Task ListenGroupAsync(AlbumsGroupCategoryViewModel group)
    {
        var albumIds = group.Items.Select(album => album.Album.Id).ToList();
        return _playbackService.PlayAlbumsAsync(albumIds);
    }

    [RelayCommand]
    private Task ListenAsync()
    {
        List<long> albumIds = Selected.Count == 0
            ? _filteredAlbums.Select(album => album.Album.Id).ToList()
            : SelectedItems.Select(album => album.Album.Id).ToList();

        return _playbackService.PlayAlbumsAsync(albumIds);
    }

    [RelayCommand]
    private async Task SurpriseMeAsync()
    {
        IReadOnlyList<AlbumViewModel> pool = Selected.Count == 0
            ? _filteredAlbums
            : SelectedItems;

        if (pool.Count == 0)
            return;

        _ = _telemetryClient.CaptureEventAsync("Event", "SurpriseMe", new Dictionary<string, object> { ["source"] = "Albums" });

        long randomAlbumId = pool[Random.Shared.Next(pool.Count)].Album.Id;
        await _playbackService.PlayAlbumsAsync([randomAlbumId]);
    }

    private bool disposedValue = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _libraryMonitor.LibraryChanged -= OnLibraryChanged;
                _libraryMonitor.LibraryRefreshed -= OnLibraryRefreshed;
                _libraryMonitor.Dispose();
                _albumProvider.Clear();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}