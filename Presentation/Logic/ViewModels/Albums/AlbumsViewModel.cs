using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Randomizer;
using Rok.Logic.Services.Player;

namespace Rok.Logic.ViewModels.Albums;

public partial class AlbumsViewModel : ObservableObject, IDisposable
{
    private readonly IMediator _mediator;
    private readonly AlbumsGroupCategory _groupService;
    private readonly AlbumsFilter _filterService;
    private readonly IPlayerService _playerService;
    private readonly ILogger<AlbumsViewModel> _logger;
    private readonly IAppOptions _appOptions;

    public List<AlbumViewModel> ViewModels { get; set; } = [];

    private bool _stateLoaded = false;

    private bool _libraryUpdated = false;

    public RangeObservableCollection<AlbumsGroupCategoryViewModel> GroupedItems { get; private set; } = [];

    private List<AlbumViewModel> _filteredAlbums = [];

    public List<GenreDto> Genres { get; private set; } = [];

    public ObservableCollection<object> Selected { get; } = [];

    public List<AlbumViewModel> SelectedItems
    {
        get
        {
            List<AlbumViewModel> list = [];

            if (Selected.Count > 0)
                list.AddRange(Selected.Select(c => (AlbumViewModel)c));

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

    private string _groupByText = AlbumsGroupCategory.KGroupByAlbum;
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
    public AsyncRelayCommand<AlbumsGroupCategoryViewModel> ListenGroupCommand { get; private set; }


    public AlbumsViewModel(IMediator mediator, AlbumsFilter albumsFilter, AlbumsGroupCategory albumsGroupCategory, IPlayerService playerService, IAppOptions appOptions, ILogger<AlbumsViewModel> logger)
    {
        _mediator = mediator;
        _groupService = albumsGroupCategory;
        _filterService = albumsFilter;
        _playerService = playerService;
        _appOptions = appOptions;
        _logger = logger;

        GroupByCommand = new RelayCommand<string>(GroupBy);
        FilterByCommand = new RelayCommand<string>(FilterBy);
        FilterByGenreCommand = new RelayCommand<long?>(FilterByGenreId);
        ListenGroupCommand = new AsyncRelayCommand<AlbumsGroupCategoryViewModel>(ListenGroupAsync);
        ListenCommand = new AsyncRelayCommand(ListenAsync);

        Messenger.Subscribe<LibraryRefreshMessage>(LibraryRefreshHandle);
        Messenger.Subscribe<AlbumUpdateMessage>(async (message) => await AlbumUpdatedMessageHandleAsync(message));
        Messenger.Subscribe<AlbumImportedMessage>((message) => _libraryUpdated = true);

        Selected.CollectionChanged += Selected_CollectionChanged;
    }


    private async Task AlbumUpdatedMessageHandleAsync(AlbumUpdateMessage message)
    {
        AlbumViewModel? albumToUpdate = ViewModels.FirstOrDefault(c => c.Album.Id == message.Id);
        AlbumDto albumDto = default!;

        ActionType action = message.Action;

        if (action == ActionType.Add && albumToUpdate != null)
            action = ActionType.Update;

        if ((action == ActionType.Update || action == ActionType.Delete) && albumToUpdate == null)
        {
            _logger.LogWarning("Album {Id} not found for update or delete.", message.Id);
            return;
        }

        if (action == ActionType.Update || action == ActionType.Add)
        {
            Result<AlbumDto> result = await _mediator.SendMessageAsync(new GetAlbumByIdQuery(message.Id));
            if (result.IsError)
            {
                _logger.LogError("Failed to retrieve album {Id} for update or delete: {ErrorMessage}", message.Id, result.Error);
                return;
            }
            else
                albumDto = result.Value!;
        }

        switch (action)
        {
            case ActionType.Add:
                AlbumViewModel viewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
                viewModel.SetData(albumDto);
                ViewModels.Add(viewModel);
                _logger.LogTrace("Album {Id} viewmodel add.", message.Id);
                break;

            case ActionType.Update:
                albumToUpdate!.SetData(albumDto);
                _logger.LogTrace("Album {Id} viewmodel updated.", message.Id);
                break;

            case ActionType.Delete:
                ViewModels.RemoveAll(c => c.Album.Id == message.Id);
                _logger.LogTrace("Album {Id} viewmodel removed.", message.Id);
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
        if (!mustLoad)
        {
            _logger.LogInformation("Albums already loaded, skipping reload.");
            return;
        }

        _libraryUpdated = false;

        if (!_stateLoaded)
            LoadState();

        await LoadGenresAsync();

        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Albums loaded"))
        {
            IEnumerable<AlbumDto> albums = await _mediator.SendMessageAsync(new GetAllAlbumsQuery());

            ViewModels = CreateAlbumsViewModels(albums);

            FilterAndSort();
        }
    }


    public void SetData(List<AlbumDto> albums)
    {
        using (PerfLogger perfLogger = new PerfLogger(_logger).Parameters("Albums loaded"))
        {
            ViewModels = CreateAlbumsViewModels(albums);

            FilterAndSort();
        }
    }


    private static List<AlbumViewModel> CreateAlbumsViewModels(IEnumerable<AlbumDto> albums)
    {
        int capacity = albums.Count();
        List<AlbumViewModel> albumViewModels = new(capacity);

        foreach (AlbumDto album in albums)
        {
            AlbumViewModel albumViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
            albumViewModel.SetData(album);
            albumViewModels.Add(albumViewModel);
        }

        return albumViewModels;
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
        IEnumerable<AlbumViewModel> filteredAlbums = ViewModels;
        foreach (string filterby in _selectedFilters)
            filteredAlbums = AlbumsFilter.Filter(filterby, filteredAlbums);

        foreach (long genreId in _selectedGenreFilters)
            filteredAlbums = AlbumsFilter.FilterByGenreId(genreId, filteredAlbums);

        _filteredAlbums = filteredAlbums.ToList();

        IEnumerable<AlbumsGroupCategoryViewModel> albums = AlbumsGroupCategory.GetGroupedItems(_groupByText, _filteredAlbums);
        GroupedItems.InitWithAddRange(albums);

        TotalCount = _filteredAlbums.Count;
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
        _appOptions.AlbumsGroupBy = _groupByText;
        _appOptions.AlbumsFilterBy = _selectedFilters;
        _appOptions.AlbumsFilterByGenresId = _selectedGenreFilters;
    }


    private void LoadState()
    {
        _stateLoaded = true;
        _groupByText = string.IsNullOrEmpty(_appOptions.AlbumsGroupBy) ? AlbumsGroupCategory.KGroupByAlbum : _appOptions.AlbumsGroupBy;
        _selectedFilters = _appOptions.AlbumsFilterBy;
        _selectedGenreFilters = _appOptions.AlbumsFilterByGenresId;
        SetFilterLabel();

        OnPropertyChanged(nameof(GroupByText));
        OnPropertyChanged(nameof(FilterByText));
    }


    private async Task ListenGroupAsync(AlbumsGroupCategoryViewModel group)
    {
        List<long> albumIds = [];
        albumIds.AddRange(group.Items.Select(album => album.Album.Id));

        await ListenAsync(albumIds);
    }


    private async Task ListenAsync()
    {
        List<long> albumIds = [];

        if (Selected.Count == 0)
            albumIds.AddRange(_filteredAlbums.Select(album => album.Album.Id));
        else
            albumIds.AddRange(SelectedItems.Select(album => album.Album.Id));

        await ListenAsync(albumIds);
    }


    private async Task ListenAsync(List<long> albumIds)
    {
        if (albumIds.Count != 0)
        {
            IEnumerable<TrackDto> tracks = await _mediator.SendMessageAsync(new GetTracksByAlbumListQuery() { AlbumsId = albumIds });

            if (albumIds.Count == 1)
                tracks = TracksRandomizer.Randomize(tracks);
            else
                tracks = ArtistBalancedTrackRandomizer.Randomize(tracks);

            _playerService.LoadPlaylist(tracks.ToList());
        }
        else
            _logger.LogDebug("No track to listen.");
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
