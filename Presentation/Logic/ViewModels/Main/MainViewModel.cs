using Microsoft.UI.Dispatching;
using Rok.Application.Features.Search.Query;
using Rok.Logic.ViewModels.Search;

namespace Rok.Logic.ViewModels.Main;

public partial class MainViewModel : ObservableObject
{
    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueue DispatcherQueue => _dispatcherQueue ??= DispatcherQueue.GetForCurrentThread();

    public RelayCommand RefreshLibraryCommand { get; private set; }

    private readonly IImport _importService;

    private readonly IMediator _mediator;

    private readonly NavigationService _navigationService;

    private bool _playerVisible = false;
    public bool PlayerVisible
    {
        get
        {
            return _playerVisible;
        }
        set
        {
            _playerVisible = value;
            OnPropertyChanged(nameof(PlayerVisible));
        }
    }

    public string Keyword { get; set; } = string.Empty;

    public RelayCommand<string> SearchCommand { get; private set; }


    public MainViewModel(IImport importService, IMediator mediator, NavigationService navigationService)
    {
        _importService = importService;
        _mediator = mediator;
        _navigationService = navigationService;

        Messenger.Subscribe<MediaChangedMessage>(OnMediaChanged);

        SearchCommand = new RelayCommand<string>(async (s) => await SearchAsync(s));
        RefreshLibraryCommand = new RelayCommand(() => RefreshLibraryAsync(), () => CanRefreshLibrary());
    }


    private void OnMediaChanged(MediaChangedMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlayerVisible = true;
        });
    }

    private bool CanRefreshLibrary()
    {
        return !_importService.UpdateInProgress;
    }

    private void RefreshLibraryAsync()
    {
        if (CanRefreshLibrary())
            _importService.StartAsync(0);
    }


    private async Task SearchAsync(string keyword)
    {
        Keyword = keyword;

        if (string.IsNullOrEmpty(keyword))
            return;

        if (Keyword.Length > 2)
        {
            SearchDto result = await _mediator.SendMessageAsync(new SearchQuery() { Name = keyword });

            bool onlyOneArtist = result.Albums.Count == 0 && result.Artists.Count == 1 && result.Tracks.Count == 0;
            bool onlyOneAlbum = result.Albums.Count > 0 && result.Artists.Count == 0 && result.Tracks.Count == 0;

            if (onlyOneArtist)
                _navigationService.NavigateToArtist(result.Artists[0].Id);
            else if (onlyOneAlbum)
                _navigationService.NavigateToAlbum(result.Albums[0].Id);
            else if (result.ResultCount > 0)
                _navigationService.NavigateToSearch(new SearchOpenArgs { SearchResult = result });
            else
                Messenger.Send(new SearchNoResultMessage());
        }
    }
}
