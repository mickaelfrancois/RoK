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

    public AlbumsPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<AlbumsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<AlbumsViewModel>();
        DataContext = ViewModel;

        Loaded += Page_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.GroupedItems.CollectionChanged += GroupedItems_CollectionChanged;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);
        UpdateVisualState();

        base.OnNavigatedTo(e);
    }


    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(grid);
        ViewModel.SaveState();

        // Cleanup bindings and handlers to avoid keeping generated binding tracking objects alive
        Dispose();

        base.OnNavigatingFrom(e);
    }


    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateItemsSource();
        ScrollStateHelper.RestoreScrollOffset(grid);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsGridView))
        {
            UpdateVisualState();
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsGroupingEnabled))
        {
            if (!ViewModel.IsGroupingEnabled && !GridZoom.IsZoomedInViewActive)
            {
                GridZoom.IsZoomedInViewActive = true;
            }

            UpdateItemsSource();
        }
    }

    private void GroupedItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateItemsSource();
    }

    private void UpdateItemsSource()
    {
        if (ViewModel.GroupedItems.Count == 0)
            return;

        grid.ItemsSource = null;
        ZoomoutCollectionGrid.ItemsSource = null;

        if (ViewModel.IsGroupingEnabled)
        {
            groupedItemsViewSource.Source = null;
            groupedItemsViewSource.IsSourceGrouped = true;
            groupedItemsViewSource.Source = ViewModel.GroupedItems;

            grid.ItemsSource = groupedItemsViewSource.View;
            ZoomoutCollectionGrid.ItemsSource = groupedItemsViewSource.View.CollectionGroups;
        }
        else
        {
            groupedItemsViewSource.Source = null;
            groupedItemsViewSource.IsSourceGrouped = false;

            grid.ItemsSource = ViewModel.GroupedItems[0].Items;
        }
    }

    private void FilterFlyout_Opened(object sender, object e)
    {
        _filterMenuBuilder.PopulateFilterMenu(filterMenu, ViewModel);
    }


    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }


    private void GridContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is AlbumViewModel item && item.Picture == null)
            item.LoadPicture();
    }

    private void GroupByFlyout_Opened(object sender, object e)
    {
        _groupByMenuBuilder.PopulateGroupByMenu(groupByMenu, ViewModel);
    }


    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
            {
                Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
                showFavoriteButtonStoryboard?.Begin();
            }
        }
    }


    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (!albumViewModel.IsFavorite)
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

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.GroupedItems.CollectionChanged -= GroupedItems_CollectionChanged;
            }

            if (grid is not null)
                grid.ItemsSource = null;

            if (ZoomoutCollectionGrid is not null)
                ZoomoutCollectionGrid.ItemsSource = null;

            if (groupedItemsViewSource is not null)
            {
                groupedItemsViewSource.Source = null;
                groupedItemsViewSource.IsSourceGrouped = false;
            }

            DataContext = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dispose in AlbumsPage");
        }

        _disposed = true;
    }
}
