using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rok.Logic.ViewModels.Albums.Interfaces;
using Rok.Logic.ViewModels.Albums.Services;

namespace Rok.Logic.ViewModels.Albums;


public partial class AlbumsViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<AlbumsViewModel> _logger;
    private readonly IAlbumProvider _albumProvider;
    private readonly IAlbumLibraryMonitor _libraryMonitor;
    private readonly AlbumsSelectionManager _selectionManager;
    private readonly AlbumsStateManager _stateManager;
    private readonly AlbumsPlaybackService _playbackService;
    private bool _stateLoaded = false;
    private bool _libraryUpdated = false;
    private List<AlbumViewModel> _filteredAlbums = [];

    public RangeObservableCollection<AlbumsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    public List<AlbumViewModel> ViewModels => _albumProvider.ViewModels;
    public List<GenreDto> Genres => _albumProvider.Genres;
    public ObservableCollection<object> Selected => _selectionManager.Selected;
    public List<AlbumViewModel> SelectedItems => _selectionManager.SelectedItems;
    public List<string> SelectedFilters => _stateManager.SelectedFilters;
    public List<long> SelectedGenreFilters => _stateManager.SelectedGenreFilters;
    public bool IsSelectedItems => _selectionManager.IsSelectedItems;

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
    public string GroupByText => _albumProvider.GetGroupByLabel(SelectedGroupBy);



    public AlbumsViewModel(IAlbumProvider albumProvider, IAlbumLibraryMonitor libraryMonitor, AlbumsSelectionManager selectionManager, AlbumsStateManager stateManager, AlbumsPlaybackService playbackService, ILogger<AlbumsViewModel> logger)
    {
        _albumProvider = albumProvider;
        _libraryMonitor = libraryMonitor;
        _selectionManager = selectionManager;
        _stateManager = stateManager;
        _playbackService = playbackService;
        _logger = logger;

        IsGridView = _stateManager.GetGridView();
        _libraryMonitor.LibraryChanged += OnLibraryChanged;
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
        FilterAndSort();
    }

    private void OnAlbumImported(object? sender, EventArgs e)
    {
        _libraryUpdated = true;
    }

    public async Task LoadDataAsync(bool forceReload)
    {
        IsGridView = _stateManager.GetGridView();

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

        FilterAndSort();
    }

    public void SetData(List<AlbumDto> albums)
    {
        _albumProvider.SetAlbums(albums);
        FilterAndSort();
    }

    private void SetFilterLabel()
    {
        if (_stateManager.SelectedFilters.Count > 0)
        {
            string lastFilter = _stateManager.SelectedFilters[^1];
            FilterByText = _albumProvider.GetFilterLabel(lastFilter);
        }
        else if (_stateManager.SelectedGenreFilters.Count > 0)
        {
            long lastGenreId = _stateManager.SelectedGenreFilters[^1];
            FilterByText = Genres.FirstOrDefault(c => c.Id == lastGenreId)?.Name ?? "";
        }
        else
        {
            FilterByText = _albumProvider.GetFilterLabel("");
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
        AlbumProviderResult result = _albumProvider.GetProcessedData(_stateManager.GroupBy, _stateManager.SelectedFilters, _stateManager.SelectedGenreFilters);

        _filteredAlbums = result.FilteredItems;
        GroupedItems.InitWithAddRange(result.Groups);
        IsGroupingEnabled = result.IsGroupingEnabled;
    }

    [RelayCommand]
    private async Task ListenGroupAsync(AlbumsGroupCategoryViewModel group)
    {
        List<long> albumIds = group.Items.Select(album => album.Album.Id).ToList();
        await _playbackService.PlayAlbumsAsync(albumIds);
    }

    [RelayCommand]
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
                _libraryMonitor.LibraryChanged -= OnLibraryChanged;
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

    #endregion
}