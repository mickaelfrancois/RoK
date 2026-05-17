using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Application.Player;
using Rok.Dialogs;
using Rok.Infrastructure;
using Rok.Services.Accessibility;
using Rok.ViewModels.Main;
using Windows.Graphics;
using Windows.System;

namespace Rok;

public sealed partial class MainWindow : Window
{
    private const int KMaxTrackListenedBeforeReviewPrompt = 10;
    private const int KMinSessionBeforeReviewPrompt = 3;
    private const int KMinDaysBeforeReviewPrompt = 45;

    private readonly NavigationService _navigationService;
    private readonly ITelemetryClient _telemetryClient;
    private readonly ResourceLoader _resourceLoader;
    private readonly IMessenger _messenger;
    private readonly List<IDisposable> _subscriptions = new();

    private PointInt32? _compactModeAppPosition;
    private PointInt32? _normalModeAppPosition;
    private SizeInt32? _normalModeAppSize;

    private bool _compactModeEnabled = false;

    private readonly IAppDbContext _dbContext;

    private readonly IAppOptions _appOptions;

    private readonly IReviewPromptEligibilityService _reviewPromptEligibilityService;

    private int _tracksListenedCount = 0;

    private bool _isOnboardingActive = false;

    private MainViewModel? _viewModel;
    public MainViewModel? ViewModel
    {
        get
        {
            if (_viewModel == null && !Windows.ApplicationModel.DesignMode.DesignModeEnabled && App.ServiceProvider != null)
                _viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();

            return _viewModel;
        }

        set => _viewModel = value;
    }

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    private static readonly Dictionary<string, Type> PageMap = new()
    {
        { "Albums", typeof(Pages.AlbumsPage) },
        { "Artists", typeof(Pages.ArtistsPage) },
        { "Album", typeof(Pages.AlbumPage) },
        { "Options", typeof(Pages.OptionsPage) },
    };



    public MainWindow(NavigationService navigationService, ITelemetryClient telemetryClient, ResourceLoader resourceLoader, IAppDbContext dbContext, IAppOptions appOptions, IReviewPromptEligibilityService reviewPromptEligibilityService, IMessenger messenger)
    {
        _navigationService = Guard.NotNull(navigationService);
        _telemetryClient = Guard.NotNull(telemetryClient);
        _resourceLoader = Guard.NotNull(resourceLoader);
        _dbContext = Guard.NotNull(dbContext);
        _appOptions = Guard.NotNull(appOptions);
        _reviewPromptEligibilityService = Guard.NotNull(reviewPromptEligibilityService);
        _messenger = Guard.NotNull(messenger);

        this.InitializeComponent();

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        }

        SplashScreen.Completed += SplashScreen_Completed;
        SplashScreen.Start();
        _ = SendMetricsAsync();

        _subscriptions.Add(_messenger.Subscribe<FullScreenMessage>((message) => FullScreenHandle(message)));
        _subscriptions.Add(_messenger.Subscribe<MediaChangedMessage>((message) => MediaChanged(message)));
        _subscriptions.Add(_messenger.Subscribe<LibraryRefreshMessage>((message) => LibraryRefreshHandle(message)));
        _subscriptions.Add(_messenger.Subscribe<SearchNoResultMessage>(async (message) => await SearchNoResultHandleAsync()));
        _subscriptions.Add(_messenger.Subscribe<CompactModeMessage>((message) => ToggleCompactMode()));

        ContentFrame.Navigated += ContentFrame_Navigated;

        TrySetSystemBackdrop();
    }

    private void TrySetSystemBackdrop()
    {
#if WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            SystemBackdrop = new MicaBackdrop();
        }
#endif
    }


    private Task SendMetricsAsync()
    {
        return _telemetryClient.CaptureEventAsync("Event", "Start");
    }


    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            navMenu.SelectedItem = sender.SettingsItem;
            ContentFrame.Navigate(typeof(Pages.OptionsPage), null, args.RecommendedNavigationTransitionInfo);
            return;
        }

        NavigationViewItem? invokedItem = sender.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Content?.ToString() == args.InvokedItem?.ToString());

        if (invokedItem == null || invokedItem.Tag == null)
            return;

        string pageName = "Rok.Pages." + ((string)invokedItem.Tag) + "Page";
        Type? pageType = Type.GetType(pageName);

        if (pageType == null)
            return;

        _navigationService.NavigateTo(pageType);
    }


    private async void NavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        _navigationService.MainFrame = ContentFrame;

        if (_dbContext.IsFirstStart)
        {
            PlaylistsSeed playlistsSeed = App.ServiceProvider.GetRequiredService<PlaylistsSeed>();
            await playlistsSeed.SeedAsync();

            _isOnboardingActive = true;
            navMenu.IsPaneVisible = false;
            ContentFrame.Navigate(typeof(Pages.WelcomePage), null, new EntranceNavigationTransitionInfo());
            return;
        }

        if (navMenu.SelectedItem == null)
        {
            navMenu.SelectedItem = albumsItem;

            if (navMenu.SelectedItem is NavigationViewItem item && item.Tag is string tag && PageMap.TryGetValue(tag, out Type? pageType))
            {
                ContentFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
            }

            if (_appOptions.RefreshLibraryAtStartup)
                LibraryRefreshButton_Tapped(this, new TappedRoutedEventArgs());
        }

        AttachGlobalShortcuts();
    }


    private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (this.ContentFrame.CanGoBack)
            this.ContentFrame.GoBack();
    }


    private void SplashScreen_Completed(object? sender, EventArgs e)
    {
        MainGrid.Visibility = Visibility.Visible;
        SplashScreen.Visibility = Visibility.Collapsed;
        SplashScreen.Completed -= SplashScreen_Completed;
    }


    private void FullScreenHandle(FullScreenMessage message)
    {
        if (message.IsFullScreen)
        {
            FullScreenGrid.Visibility = Visibility.Visible;
            MainGrid.Visibility = Visibility.Collapsed;
        }
        else
        {
            FullScreenGrid.Visibility = Visibility.Collapsed;
            MainGrid.Visibility = Visibility.Visible;
        }
    }


    private void ToggleCompactMode()
    {
        if (_compactModeEnabled)
        {
            _compactModeAppPosition = AppWindow.Position;

            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

            if (_normalModeAppSize.HasValue)
                AppWindow.Resize(_normalModeAppSize.Value);

            if (_normalModeAppPosition.HasValue)
                AppWindow.Move(_normalModeAppPosition.Value);

            gridCompactScreen.Visibility = Visibility.Collapsed;
            MainGrid.Visibility = Visibility.Visible;

            _compactModeEnabled = false;
        }
        else
        {
            _normalModeAppPosition = AppWindow.Position;
            _normalModeAppSize = AppWindow.ClientSize;

            AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
            AppWindow.Resize(new SizeInt32 { Width = 360, Height = 420 });

            if (_compactModeAppPosition.HasValue)
                AppWindow.Move(_compactModeAppPosition.Value);

            gridCompactScreen.Visibility = Visibility.Visible;
            MainGrid.Visibility = Visibility.Collapsed;

            _compactModeEnabled = true;
        }
    }


    private void MediaChanged(MediaChangedMessage message)
    {
        _dispatcherQueue.TryEnqueue(async () =>
        {
            await fullscreen.TrackChangedAsync(message.NewTrack, message.PreviousTrack);

            if (message.NewTrack != null)
            {
                _tracksListenedCount++;
                if (_tracksListenedCount == KMaxTrackListenedBeforeReviewPrompt)
                    await TryShowReviewPromptAsync();
            }
        });
    }


    private void LibraryRefreshButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel!.RefreshLibraryCommand.CanExecute(null))
            ViewModel.RefreshLibraryCommand.Execute(null);
    }


    private void LibraryRefreshHandle(LibraryRefreshMessage msg)
    {
        Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue = _navigationService?.MainFrame?.DispatcherQueue;
        if (dispatcherQueue == null || libraryRefreshButton == null || IconRotation == null || _resourceLoader == null)
            return;

        dispatcherQueue.TryEnqueue(() =>
        {
            if (msg.ProcessState == LibraryRefreshMessage.EState.Running)
            {
                IconRotation.Begin();
                libraryRefreshButton.Content = msg.ProcessMessage;
            }
            else if (msg.ProcessState == LibraryRefreshMessage.EState.Unchanged)
            {
                if (!string.IsNullOrWhiteSpace(msg.ProcessMessage))
                    libraryRefreshButton.Content = msg.ProcessMessage;
            }
            else if (msg.ProcessState == LibraryRefreshMessage.EState.CleanData)
            {
                libraryRefreshButton.Content = _resourceLoader.GetString("genCleanData");
            }
            else if (msg.ProcessState == LibraryRefreshMessage.EState.UpdateData)
            {
                libraryRefreshButton.Content = _resourceLoader.GetString("genUpdateData");
            }
            else if (msg.ProcessState == LibraryRefreshMessage.EState.Stop || msg.ProcessState == LibraryRefreshMessage.EState.Cancelled)
            {
                if (IconRotation.GetCurrentState() != Microsoft.UI.Xaml.Media.Animation.ClockState.Stopped)
                {
                    IconRotation.Stop();
                    libraryRefreshButton.Content = _resourceLoader.GetString("genRefreshLibrary");
                }
            }
        });
    }


    private async Task SearchNoResultHandleAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Title = _resourceLoader.GetString("genSearchNoResult"),
            CloseButtonText = "Ok",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }


    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SuggestionItem item)
        {
            ViewModel!.SelectSuggestionCommand.Execute(item.Data);
            sender.Text = string.Empty;
        }
        else if (!string.IsNullOrEmpty(args.QueryText))
        {
            ViewModel!.SearchCommand.Execute(args.QueryText);
        }
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string query = sender.Text;

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                sender.ItemsSource = null;
                return;
            }

            await ViewModel!.SearchSuggestions.UpdateSuggestionsAsync(query);

            List<object> suggestions = [];
            foreach (AlbumDto album in ViewModel.SearchSuggestions.AlbumSuggestions)
                suggestions.Add(new SuggestionItem { Type = "Album", Name = album.Name, Data = album, IconGlyph = "\uE93C" });

            foreach (ArtistDto artist in ViewModel.SearchSuggestions.ArtistSuggestions)
                suggestions.Add(new SuggestionItem { Type = "Artist", Name = artist.Name, Data = artist, IconGlyph = "\uE716" });

            foreach (TrackDto track in ViewModel.SearchSuggestions.TrackSuggestions)
                suggestions.Add(new SuggestionItem { Type = "Track", Name = track.Title, Data = track, IconGlyph = "\uE8D6" });

            sender.ItemsSource = suggestions;
        }
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SuggestionItem item)
        {
            sender.Text = item.Name;
        }
    }


    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (_isOnboardingActive && e.Content is not Pages.WelcomePage)
        {
            _isOnboardingActive = false;
            navMenu.IsPaneVisible = true;
            AttachGlobalShortcuts();
        }

        if (e.Content is Pages.OptionsPage)
        {
            navMenu.SelectedItem = navMenu.SettingsItem;
            return;
        }

        // Ensure that the navigation menu selection is cleared if the navigated page is not one of the expected pages.
        Type[] menuPages =
        [
            typeof(Pages.ArtistsPage),
            typeof(Pages.AlbumsPage),
            typeof(Pages.TracksPage),
            typeof(Pages.PlaylistsPage),
            typeof(Pages.InsightsPage),
            typeof(Pages.OptionsPage),
            typeof(Pages.ListeningPage)
        ];

        if (!menuPages.Contains(e.Content.GetType()))
            navMenu.SelectedItem = null;
    }


    private void ToggleTheme_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ThemeManager.Toggle();
    }


    private void AppLogo_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Storyboard storyboard = (Storyboard)navMenu.Resources["AppLogoHoverStoryboard"];
        storyboard.Begin();
    }

    private void AppLogo_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Storyboard storyboard = (Storyboard)navMenu.Resources["AppLogoExitStoryboard"];
        storyboard.Begin();
    }


    private async Task TryShowReviewPromptAsync()
    {
        if (_reviewPromptEligibilityService.ShouldShowReviewPrompt(
            _tracksListenedCount,
            KMaxTrackListenedBeforeReviewPrompt,
            KMinSessionBeforeReviewPrompt,
            KMinDaysBeforeReviewPrompt))
        {
            await ShowReviewPromptAsync();
        }
    }

    private async Task ShowReviewPromptAsync()
    {
        _appOptions.ReviewLastPromptDate = DateTimeOffset.UtcNow;

        bool? result = await ReviewPrompt.ShowAsync(
            "\uE734",
            _resourceLoader.GetString("reviewSentimentGateTitle"),
            _resourceLoader.GetString("reviewSentimentGateContent"),
            _resourceLoader.GetString("reviewSentimentGateYes"),
            _resourceLoader.GetString("reviewSentimentGateNo"));

        if (result == null)
            return;

        if (result == true)
        {
            _appOptions.HasRated = true;

            bool? reviewResult = await ReviewPrompt.ShowAsync(
                "\uE735",
                _resourceLoader.GetString("reviewPositiveTitle"),
                _resourceLoader.GetString("reviewPositiveContent"),
                _resourceLoader.GetString("reviewPositiveLeave"),
                _resourceLoader.GetString("reviewPositiveLater"));

            if (reviewResult == true)
            {
                await Launcher.LaunchUriAsync(new Uri($"ms-windows-store://review/?ProductId=9NX19R28Q92S"));
            }
        }
        else
        {
            bool? feedbackResult = await ReviewPrompt.ShowAsync(
                "\uE939",
                _resourceLoader.GetString("reviewNegativeTitle"),
                _resourceLoader.GetString("reviewNegativeContent"),
                _resourceLoader.GetString("reviewNegativeSend"),
                _resourceLoader.GetString("reviewNegativeClose"));

            if (feedbackResult == true)
                await Launcher.LaunchUriAsync(new Uri("https://github.com/mickaelfrancois/RoK/issues/new"));
        }
    }


    private void AttachGlobalShortcuts()
    {
        KeyboardShortcutInstaller installer = App.ServiceProvider.GetRequiredService<KeyboardShortcutInstaller>();

        albumsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenAlbums, OnOpenAlbumsAccelerator));
        artistsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenArtists, OnOpenArtistsAccelerator));
        tracksItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenTracks, OnOpenTracksAccelerator));
        playlistItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenPlaylists, OnOpenPlaylistsAccelerator));
        insightsItem.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenInsights, OnOpenInsightsAccelerator));

        if (Content is FrameworkElement root)
        {
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.OpenListening, OnOpenListeningAccelerator));
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.FocusSearch, OnFocusSearchAccelerator));
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.Help, OnHelpAccelerator));
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.ToggleFullScreen, OnToggleFullScreenAccelerator));
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.ToggleCompact, OnToggleCompactAccelerator));
            root.KeyboardAccelerators.Add(installer.Build(ShortcutId.Back, OnBackAccelerator));
        }
    }


    private bool IsInFullScreenMode() => FullScreenGrid.Visibility == Visibility.Visible;


    private bool IsInCompactMode() => gridCompactScreen.Visibility == Visibility.Visible;


    private void EnsureNormalMode()
    {
        if (IsInFullScreenMode())
            _messenger.Send(new FullScreenMessage(false));

        if (IsInCompactMode())
            _messenger.Send(new CompactModeMessage());
    }


    private void NavigateToPage(Type pageType, NavigationViewItem? menuItem)
    {
        EnsureNormalMode();

        if (menuItem != null)
            navMenu.SelectedItem = menuItem;

        _navigationService.NavigateTo(pageType);
    }


    private void OnOpenAlbumsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToPage(typeof(Pages.AlbumsPage), albumsItem);
    }


    private void OnOpenArtistsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToPage(typeof(Pages.ArtistsPage), artistsItem);
    }


    private void OnOpenTracksAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToPage(typeof(Pages.TracksPage), tracksItem);
    }


    private void OnOpenPlaylistsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToPage(typeof(Pages.PlaylistsPage), playlistItem);
    }


    private void OnOpenInsightsAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateToPage(typeof(Pages.InsightsPage), insightsItem);
    }


    private void OnOpenListeningAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        IPlayerService playerService = App.ServiceProvider.GetRequiredService<IPlayerService>();

        if (playerService.CurrentTrack == null)
            return;

        NavigateToPage(typeof(Pages.ListeningPage), menuItem: null);
    }


    private void OnFocusSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        EnsureNormalMode();
        searchBox.Focus(FocusState.Keyboard);
    }


    private async void OnHelpAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        KeyboardShortcutsDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }


    private void OnToggleFullScreenAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _messenger.Send(new FullScreenMessage(!IsInFullScreenMode()));
    }


    private void OnToggleCompactAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _messenger.Send(new CompactModeMessage());
    }


    private void OnBackAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsInFullScreenMode())
        {
            _messenger.Send(new FullScreenMessage(false));
            args.Handled = true;
            return;
        }

        if (IsInCompactMode())
        {
            _messenger.Send(new CompactModeMessage());
            args.Handled = true;
            return;
        }

        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
            args.Handled = true;
            return;
        }

        args.Handled = false;
    }
}