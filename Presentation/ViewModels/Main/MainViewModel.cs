using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.Application.Features.Search.Query;
using Rok.ViewModels.Search;

namespace Rok.ViewModels.Main;

public partial class MainViewModel : ObservableObject
{
    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueue DispatcherQueue => _dispatcherQueue ??= DispatcherQueue.GetForCurrentThread();

    private readonly IImport _importService;
    private readonly IMediator _mediator;
    private readonly NavigationService _navigationService;
    private readonly IAppOptions _appOptions;
    private readonly IDialogService _dialogService;
    private readonly ResourceLoader _resourceLoader;

    [ObservableProperty]
    public partial bool PlayerVisible { get; set; }

    public string Keyword { get; set; } = string.Empty;

    public SearchSuggestionsViewModel SearchSuggestions { get; }

    public MainViewModel(IImport importService, IMediator mediator, NavigationService navigationService, IAppOptions appOptions, IDialogService dialogService, ResourceLoader resourceLoader, SearchSuggestionsViewModel searchSuggestions)
    {
        _importService = importService;
        _mediator = mediator;
        _navigationService = navigationService;
        _appOptions = appOptions;
        _dialogService = dialogService;
        _resourceLoader = resourceLoader;
        SearchSuggestions = searchSuggestions;

        Messenger.Subscribe<MediaChangedMessage>(OnMediaChanged);
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
        if (_appOptions.LibraryTokens.Count == 0)
        {
            string message = _resourceLoader.GetString("NoLibraryFoldersMessage");
            string title = _resourceLoader.GetString("NoLibraryFoldersTitleMessage");
            _dialogService.ShowTextAsync(title, message);
            return false;
        }

        return !_importService.UpdateInProgress;
    }

    [RelayCommand]
    private void RefreshLibrary()
    {
        if (CanRefreshLibrary())
            _importService.StartAsync(0);
    }

    [RelayCommand]
    private async Task SearchAsync(string keyword)
    {
        Keyword = keyword;

        if (string.IsNullOrEmpty(keyword))
        {
            SearchSuggestions.ClearSuggestions();
            return;
        }

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

    [RelayCommand]
    private void SelectSuggestion(object suggestion)
    {
        SearchSuggestions.ClearSuggestions();

        switch (suggestion)
        {
            case AlbumDto album:
                _navigationService.NavigateToAlbum(album.Id);
                break;
            case ArtistDto artist:
                _navigationService.NavigateToArtist(artist.Id);
                break;
            case TrackDto track:
                _navigationService.NavigateToTrack(track.Id);
                break;
        }
    }
}
