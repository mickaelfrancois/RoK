using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.Application.Features.Tracks.Query;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Rok.Logic.ViewModels.Start;


public partial class StartViewModel : ObservableObject
{
    private const int KAlbumMinimumBeforeUse = 30;

    private readonly IAlbumPicture _albumPicture;
    private readonly NavigationService _navigationService;
    private readonly IMediator _mediator;
    private readonly IImport _importService;
    private readonly IAppOptions _appOptions;
    private readonly ISettingsFile _settingsFile;

    private readonly Lock _lock = new();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public RangeObservableCollection<AlbumImportedModel> AlbumsImported { get; } = new();

    [ObservableProperty]
    public partial bool LibraryRefreshRunning { get; set; } = true;

    [ObservableProperty]
    public partial bool ErrorOccurred { get; set; } = false;



    public StartViewModel(IAlbumPicture albumPicture, ISettingsFile settingsFile, NavigationService navigationService, IMediator mediator, IImport importService, IAppOptions appOptions)
    {
        _albumPicture = albumPicture;
        _settingsFile = settingsFile;
        _navigationService = navigationService;
        _mediator = mediator;
        _importService = importService;
        _appOptions = appOptions;

        Messenger.Subscribe<LibraryRefreshMessage>(async (message) => await LibraryRefreshChange(message));
        Messenger.Subscribe<AlbumImportedMessage>(AlbumImported);
    }


    private void UnregisterEvents()
    {
        Messenger.Unsubscribe<LibraryRefreshMessage>(async (message) => await LibraryRefreshChange(message));
        Messenger.Unsubscribe<AlbumImportedMessage>(AlbumImported);
    }


    private async Task LibraryRefreshChange(LibraryRefreshMessage message)
    {
        if (message.ProcessState == LibraryRefreshMessage.EState.Running)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                LibraryRefreshRunning = true;
            });
        }

        if (message.ProcessState == LibraryRefreshMessage.EState.Stop)
        {
            int trackCount = await _mediator.SendMessageAsync(new GetTracksCountQuery());

            _dispatcherQueue.TryEnqueue(() =>
            {
                LibraryRefreshRunning = false;

                if (trackCount == 0)
                {
                    ErrorOccurred = true;
                }
                else
                {
                    UnregisterEvents();
                    _navigationService.NavigateToAlbums();
                }
            });
        }
    }


    private void AlbumImported(AlbumImportedMessage message)
    {
        BitmapImage cover;

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_albumPicture.PictureFileExists(message.AlbumPath))
            {
                string filePath = _albumPicture.GetPictureFile(message.AlbumPath);
                cover = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                cover = (BitmapImage)Microsoft.UI.Xaml.Application.Current.Resources["albumCoverFallback"];
            }

            lock (_lock)
            {
                AlbumsImported.Insert(0, new AlbumImportedModel
                {
                    Name = message.Name,
                    ArtistName = message.ArtistName,
                    AlbumPath = message.AlbumPath,
                    Picture = cover
                });

                if (AlbumsImported.Count > KAlbumMinimumBeforeUse)
                {
                    UnregisterEvents();
                    _navigationService.NavigateToAlbums();
                }
            }
        });
    }


    [RelayCommand]
    private async Task AddLibraryFolderAsync(StorageFolder folder)
    {
        string token = Guid.NewGuid().ToString();
        StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);

        if (!_appOptions.LibraryTokens.Contains(token))
        {
            _appOptions.LibraryTokens.Clear(); // In start process, we clear all library path as we haven't found music in.
            _appOptions.LibraryTokens.Add(token);
            await _settingsFile.SaveAsync(_appOptions);

            ErrorOccurred = false;
            LibraryRefreshRunning = true;

            _importService.StartAsync(0);
        }
    }
}
