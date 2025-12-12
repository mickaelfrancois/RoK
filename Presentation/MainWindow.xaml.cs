using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Infrastructure;
using Rok.Logic.ViewModels.Main;

namespace Rok
{
    public sealed partial class MainWindow : Window
    {
        private readonly NavigationService _navigationService;

        private readonly ResourceLoader _resourceLoader;

        private bool _compactModeEnabled = false;

        private readonly IAppDbContext _dbContext;

        private readonly IAppOptions _appOptions;

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

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private static readonly Dictionary<string, Type> PageMap = new()
        {
            { "Albums", typeof(Pages.AlbumsPage) },
            { "Artists", typeof(Pages.ArtistsPage) },
            { "Album", typeof(Pages.AlbumPage) },
            { "Options", typeof(Pages.OptionsPage) },
        };



        public MainWindow(NavigationService navigationService, ResourceLoader resourceLoader, IAppDbContext dbContext, IAppOptions appOptions)
        {
            _navigationService = Guard.Against.Null(navigationService);
            _resourceLoader = Guard.Against.Null(resourceLoader);
            _dbContext = Guard.Against.Null(dbContext);
            _appOptions = Guard.Against.Null(appOptions);

            this.InitializeComponent();

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
                AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
            }

            SplashScreen.Completed += SplashScreen_Completed;
            SplashScreen.Start();

            Messenger.Subscribe<FullScreenMessage>((message) => FullScreenHandle(message));
            Messenger.Subscribe<MediaChangedMessage>((message) => MediaChanged());
            Messenger.Subscribe<LibraryRefreshMessage>((message) => LibraryRefreshHandle(message));
            Messenger.Subscribe<SearchNoResultMessage>(async (message) => await SearchNoResultHandleAsync());
            Messenger.Subscribe<CompactModeMessage>((message) => ToggleCompactMode());

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


        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            _navigationService.MainFrame = ContentFrame;

            if (_dbContext.IsFirstStart)
            {
                PlaylistsSeed playlistsSeed = App.ServiceProvider.GetRequiredService<PlaylistsSeed>();
                playlistsSeed.SeedAsync().GetAwaiter().GetResult();

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
                AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                gridCompactScreen.Visibility = Visibility.Collapsed;
                MainGrid.Visibility = Visibility.Visible;
                _compactModeEnabled = false;
            }
            else
            {
                AppWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);
                AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 360, Height = 420 });
                gridCompactScreen.Visibility = Visibility.Visible;
                MainGrid.Visibility = Visibility.Collapsed;
                _compactModeEnabled = true;
            }
        }


        private void MediaChanged()
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                await fullscreen.TrackChangedAsync();
            });
        }


        private void LibraryRefreshButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel!.RefreshLibraryCommand.CanExecute(null))
                ViewModel.RefreshLibraryCommand.Execute(null);
        }


        private void LibraryRefreshHandle(LibraryRefreshMessage msg)
        {
            DispatcherQueue? dispatcherQueue = _navigationService?.MainFrame?.DispatcherQueue;
            if (dispatcherQueue == null || libraryRefreshButton == null || IconRotation == null || _resourceLoader == null)
                return;

            dispatcherQueue.TryEnqueue(() =>
            {
                if (msg.ProcessState == LibraryRefreshMessage.EState.Running)
                {
                    IconRotation.Begin();
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
                else
                {
                    libraryRefreshButton.Content = msg.ProcessMessage;
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
            if (!string.IsNullOrEmpty(args.QueryText))
                ViewModel!.SearchCommand.Execute(args.QueryText);
        }


        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
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
    }
}
