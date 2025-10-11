﻿using Microsoft.UI.Dispatching;
using Rok.Application.Features.Tracks.Query;
using System.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Rok.Logic.ViewModels.Start;


public class StartViewModel : ObservableObject
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

    public bool LibraryRefreshRunning { get; set; } = true;

    public bool ErrorOccurred { get; set; } = false;

    public RelayCommand<StorageFolder> AddLibraryFolderCommand { get; private set; }


    public StartViewModel(IAlbumPicture albumPicture, ISettingsFile settingsFile, NavigationService navigationService, IMediator mediator, IImport importService, IAppOptions appOptions)
    {
        _albumPicture = albumPicture;
        _settingsFile = settingsFile;
        _navigationService = navigationService;
        _mediator = mediator;
        _importService = importService;
        _appOptions = appOptions;

        AddLibraryFolderCommand = new RelayCommand<StorageFolder>(AddLibraryFolder);

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
                OnPropertyChanged(nameof(LibraryRefreshRunning));
            });
        }

        if (message.ProcessState == LibraryRefreshMessage.EState.Stop)
        {
            int trackCount = await _mediator.SendMessageAsync(new GetTracksCountQuery());

            _dispatcherQueue.TryEnqueue(() =>
            {
                LibraryRefreshRunning = false;
                OnPropertyChanged(nameof(LibraryRefreshRunning));

                if (trackCount == 0)
                {
                    ErrorOccurred = true;
                    OnPropertyChanged(nameof(ErrorOccurred));
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


    private void AddLibraryFolder(StorageFolder folder)
    {
        string token = Guid.NewGuid().ToString();
        StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);

        if (!_appOptions.LibraryTokens.Contains(token))
        {
            _appOptions.LibraryPath.Clear(); // In start process, we clear all library path as we haven't found music in.
            _appOptions.LibraryTokens.Add(token);
            _appOptions.LibraryPath.Add(folder.Path);
            _settingsFile.Save(_appOptions);

            ErrorOccurred = false;
            LibraryRefreshRunning = true;

            OnPropertyChanged(nameof(ErrorOccurred));
            OnPropertyChanged(nameof(LibraryRefreshRunning));

            _importService.StartAsync(0);
        }
    }
}
