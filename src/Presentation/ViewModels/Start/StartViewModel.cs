using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Interfaces.Pictures;
using Rok.Import.Services;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace Rok.ViewModels.Start;

public partial class StartViewModel : ObservableObject
{
    private const int KDisplayIntervalMs = 300;

    // Must stay <= ImportMessageThrottler.MaxMessagesBeforeThrottle.
    // If unlock > throttle, AlbumImportedMessage stops arriving before the threshold is reached
    // and navigation falls back to the LibraryRefreshMessage Stop handler without notification.
    private const int KMinAlbumsToUnlockApp = 100;

    private readonly IAlbumPicture _albumPicture;
    private readonly NavigationService _navigationService;
    private readonly IMediator _mediator;
    private readonly IImport _importService;
    private readonly IAppOptions _appOptions;
    private readonly ISettingsFile _settingsFile;
    private readonly ITelemetryClient _telemetryClient;

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private readonly Queue<AlbumImportedModel> _pendingAlbums = new();
    private readonly DispatcherQueueTimer _displayTimer;
    private readonly string _albumsImportedMessage;
    private readonly string _importBackgroundTitle;
    private readonly string _importBackgroundMessage;
    private readonly string _errorAccessDenied;
    private readonly string _errorNoAudioFiles;

    public RangeObservableCollection<AlbumImportedModel> AlbumsImported { get; } = new();

    [ObservableProperty]
    public partial bool LibraryRefreshRunning { get; set; } = true;

    [ObservableProperty]
    public partial bool ErrorOccurred { get; set; } = false;

    [ObservableProperty]
    public partial string ImportProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double ImportProgress { get; set; } = 0;

    [ObservableProperty]
    public partial string? ErrorBannerMessage { get; set; }

    public StartViewModel(IAlbumPicture albumPicture, ISettingsFile settingsFile, NavigationService navigationService, IResourceService resourceService, IMediator mediator, IImport importService, IAppOptions appOptions, ITelemetryClient telemetryClient)
    {
        Debug.Assert(
            ImportMessageThrottler.MaxMessagesBeforeThrottle >= KMinAlbumsToUnlockApp,
            $"Throttle ({ImportMessageThrottler.MaxMessagesBeforeThrottle}) must be >= unlock threshold ({KMinAlbumsToUnlockApp}).");

        _albumPicture = albumPicture;
        _settingsFile = settingsFile;
        _navigationService = navigationService;
        _mediator = mediator;
        _importService = importService;
        _appOptions = appOptions;
        _telemetryClient = telemetryClient;

        _albumsImportedMessage = resourceService.GetString("AlbumsImported");
        _importBackgroundTitle = resourceService.GetString("notification_import_background_title");
        _importBackgroundMessage = resourceService.GetString("notification_import_background_message");
        _errorAccessDenied = resourceService.GetString("startViewErrorAccessDenied");
        _errorNoAudioFiles = resourceService.GetString("startViewErrorNoAudio");

        _displayTimer = _dispatcherQueue.CreateTimer();
        _displayTimer.Interval = TimeSpan.FromMilliseconds(KDisplayIntervalMs);
        _displayTimer.Tick += OnDisplayTimerTick;
        _displayTimer.Start();

        Messenger.Subscribe<LibraryRefreshMessage>(OnLibraryRefreshMessage);
        Messenger.Subscribe<AlbumImportedMessage>(AlbumImported);
    }

    private void OnLibraryRefreshMessage(LibraryRefreshMessage message) => _ = LibraryRefreshChangeAsync(message);

    private void UnregisterEvents()
    {
        _displayTimer.Stop();
        Messenger.Unsubscribe<LibraryRefreshMessage>(OnLibraryRefreshMessage);
        Messenger.Unsubscribe<AlbumImportedMessage>(AlbumImported);
    }

    private void OnDisplayTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_pendingAlbums.Count == 0)
            return;

        AlbumImportedModel album = _pendingAlbums.Dequeue();
        AlbumsImported.Insert(0, album);
        ImportProgressText = $"{AlbumsImported.Count} {_albumsImportedMessage}";
        ImportProgress = Math.Min(AlbumsImported.Count * 100.0 / KMinAlbumsToUnlockApp, 100);

        if (AlbumsImported.Count >= KMinAlbumsToUnlockApp)
        {
            Messenger.Send(new ShowNotificationMessage
            {
                Title = _importBackgroundTitle,
                Message = _importBackgroundMessage,
                Type = NotificationType.Informational
            });
            UnregisterEvents();
            _navigationService.NavigateToAlbums();
        }
    }

    private async Task LibraryRefreshChangeAsync(LibraryRefreshMessage message)
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
                    if (_appOptions.LibraryTokens.Count > 0)
                    {
                        ErrorBannerMessage = _errorNoAudioFiles;
                        _ = _telemetryClient.CaptureEventAsync("Onboarding", "NoAudioFiles");
                    }
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
        _dispatcherQueue.TryEnqueue(() =>
        {
            BitmapImage cover;

            if (_albumPicture.PictureFileExists(message.AlbumPath))
            {
                string filePath = _albumPicture.GetPictureFile(message.AlbumPath);
                cover = new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                cover = (BitmapImage)Microsoft.UI.Xaml.Application.Current.Resources["albumCoverFallback"];
            }

            _pendingAlbums.Enqueue(new AlbumImportedModel
            {
                Name = message.Name,
                ArtistName = message.ArtistName,
                AlbumPath = message.AlbumPath,
                Picture = cover
            });
        });
    }

    public void StartInitialScan()
    {
        if (_appOptions.LibraryTokens.Count == 0)
        {
            LibraryRefreshRunning = false;
            ErrorOccurred = true;
            _ = _telemetryClient.CaptureEventAsync("Onboarding", "NoFolderConfigured");
            return;
        }

        _importService.Start(0);
    }

    [RelayCommand]
    private async Task AddLibraryFolderAsync(StorageFolder folder)
    {
        FolderValidationResult validationResult = await FolderValidator.ValidateAsync(folder.Path);

        if (validationResult == FolderValidationResult.AccessDenied)
        {
            _dispatcherQueue.TryEnqueue(() => ErrorBannerMessage = _errorAccessDenied);
            _ = _telemetryClient.CaptureEventAsync("Onboarding", "FolderAccessDenied");
            return;
        }

        if (validationResult == FolderValidationResult.NoAudioFiles)
        {
            _dispatcherQueue.TryEnqueue(() => ErrorBannerMessage = _errorNoAudioFiles);
            _ = _telemetryClient.CaptureEventAsync("Onboarding", "FolderNoAudioFiles");
            return;
        }

        string token = Guid.NewGuid().ToString();
        StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);

        _appOptions.LibraryTokens.Clear();
        _appOptions.LibraryTokens.Add(token);
        await _settingsFile.SaveAsync(_appOptions);

        _dispatcherQueue.TryEnqueue(() =>
        {
            ErrorBannerMessage = null;
            ErrorOccurred = false;
            LibraryRefreshRunning = true;
        });

        _importService.Start(0);
    }
}