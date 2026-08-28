using System.ComponentModel;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.ViewModels.Album;
using Rok.ViewModels.Albums;

namespace Rok.Pages;

public sealed partial class AlbumsPage : Page, IDisposable
{
    private readonly ILogger<AlbumsPage> _logger;

    public AlbumsViewModel ViewModel { get; set; }

    private readonly AlbumsFilterMenuBuilder _filterMenuBuilder = new();
    private readonly AlbumsGroupByMenuBuilder _groupByMenuBuilder = new();

    private bool _disposed;
    private bool _pageLoaded;

    private readonly AnimatedNumberHelper _countAnimation;
    private readonly AnimatedNumberHelper _durationAnimation;

    private readonly GroupedItemsSourceBinder _binder;


    public AlbumsPage()
    {
        InitializeComponent();

        _countAnimation = new AnimatedNumberHelper(t => albumCountRun.Text = t);
        _durationAnimation = new AnimatedNumberHelper(t => albumDurationRun.Text = t);

        _logger = App.ServiceProvider.GetRequiredService<ILogger<AlbumsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<AlbumsViewModel>();

        _binder = new GroupedItemsSourceBinder(grid, ZoomoutCollectionGrid, groupedItemsViewSource, ViewModel.GroupedItems, _logger);

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.GroupedItems.CollectionChanged += GroupedItems_CollectionChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            await ViewModel.LoadDataAsync(forceReload: false);
            UpdateVisualState();
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to AlbumsPage failed");
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(grid);
        ViewModel.SaveState();

        _pageLoaded = false;
        Dispose();

        base.OnNavigatingFrom(e);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _pageLoaded = true;
        UpdateItemsSource();
        ScrollStateHelper.RestoreScrollOffset(grid);
        _countAnimation.AnimateTo(ViewModel.Count);
        _durationAnimation.AnimateTo(ViewModel.DurationText);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // The page has left the visual tree: no container is realized any more, so releasing the
        // sources here can no longer feed a null item to the generated bindings.
        _binder.Release();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Count))
        {
            if (_pageLoaded)
                _countAnimation.AnimateTo(ViewModel.Count);
            return;
        }

        if (e.PropertyName == nameof(ViewModel.DurationText))
        {
            if (_pageLoaded)
                _durationAnimation.AnimateTo(ViewModel.DurationText);
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsGridView))
        {
            UpdateVisualState();
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsGroupingEnabled))
        {
            // The flag is published before the data, so the rewiring happens on the collection
            // Reset that follows. Only the zoom state is handled here.
            if (!ViewModel.IsGroupingEnabled && !GridZoom.IsZoomedInViewActive)
                GridZoom.IsZoomedInViewActive = true;
        }
    }

    private void GroupedItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.GroupedItems.Count == 0 && !GridZoom.IsZoomedInViewActive)
            GridZoom.IsZoomedInViewActive = true;

        UpdateItemsSource();
    }

    private void UpdateItemsSource()
    {
        _binder.Apply(ViewModel.IsGroupingEnabled, ViewModel.GroupedItems.Count > 0 ? ViewModel.GroupedItems[0].Items : null);
    }

    private void FilterFlyout_Opened(object sender, object e)
    {
        _filterMenuBuilder.PopulateFilterMenu(filterMenu, ViewModel);
    }

    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void GroupListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: AlbumsGroupCategoryViewModel group })
            ViewModel.ListenGroupCommand.Execute(group);
    }

    private void GridContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        // Release storyboard holds so {x:Bind} values re-apply when the container is reused for another album.
        if (args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Grid root)
            StopHoverStoryboards(root);
    }

    private static void StopHoverStoryboards(Grid root)
    {
        StopStoryboard(root, "ShowArtistNameStoryboard");
        StopStoryboard(root, "ShowSubTitleStoryboard");
        StopStoryboard(root, "ShowFavoriteButtonStoryboard");
        StopStoryboard(root, "HideFavoriteButtonStoryboard");
    }

    private static void StopStoryboard(Grid root, string key)
    {
        if (root.Resources.TryGetValue(key, out object? value) && value is Storyboard storyboard)
            storyboard.Stop();
    }

    private void GroupByFlyout_Opened(object sender, object e)
    {
        _groupByMenuBuilder.PopulateGroupByMenu(groupByMenu, ViewModel);
    }

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();

            Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
            showFavoriteButtonStoryboard?.Begin();
        }
    }

    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (albumViewModel.IsFavorite)
            {
                // Release the animated values so the {x:Bind} on Opacity stays authoritative for favorites.
                StopStoryboard(gridItem, "ShowFavoriteButtonStoryboard");
                StopStoryboard(gridItem, "HideFavoriteButtonStoryboard");
            }
            else
            {
                Storyboard? hideFavoriteButtonStoryboard = gridItem.Resources["HideFavoriteButtonStoryboard"] as Storyboard;
                hideFavoriteButtonStoryboard?.Begin();
            }
        }
    }

    private void UpdateVisualState()
    {
        VisualStateManager.GoToState(this, ViewModel.IsGridView ? "GridViewState" : "ListViewState", true);
    }


    public void Dispose()
    {
        if (!this.DispatcherQueue.HasThreadAccess)
        {
            this.DispatcherQueue.TryEnqueue(() => Dispose());
            return;
        }

        if (Interlocked.Exchange(ref _disposed, true))
            return;

        try
        {
            Loaded -= Page_Loaded;

            _countAnimation.Dispose();
            _durationAnimation.Dispose();

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.GroupedItems.CollectionChanged -= GroupedItems_CollectionChanged;
            }

            // The sources are released in Page_Unloaded, once the page has left the visual tree.
            // Detaching them here would recycle containers while the page is still on screen.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dispose in AlbumsPage");
        }
    }
}