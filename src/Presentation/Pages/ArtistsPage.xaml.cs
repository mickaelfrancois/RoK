using System.ComponentModel;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.ViewModels.Artist;
using Rok.ViewModels.Artists;

namespace Rok.Pages;

public sealed partial class ArtistsPage : Page, IDisposable
{
    private readonly ILogger<ArtistsPage> _logger;

    public ArtistsViewModel ViewModel { get; set; }

    private readonly ArtistsFilterMenuBuilder _filterMenuBuilder = new();
    private readonly ArtistsGroupByMenuBuilder _groupByMenuBuilder = new();

    private bool _disposed;
    private bool _pageLoaded;

    private readonly AnimatedNumberHelper _countAnimation;
    private readonly AnimatedNumberHelper _durationAnimation;



    public ArtistsPage()
    {
        InitializeComponent();

        _countAnimation = new AnimatedNumberHelper(t => artistCountRun.Text = t);
        _durationAnimation = new AnimatedNumberHelper(t => artistDurationRun.Text = t);

        _logger = App.ServiceProvider.GetRequiredService<ILogger<ArtistsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<ArtistsViewModel>();

        Loaded += Page_Loaded;
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
            _logger.LogError(ex, "Navigation to ArtistsPage failed");
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

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel)
        {
            Storyboard? showGenreStoryboard = gridItem.Resources["ShowGenreNameStoryboard"] as Storyboard;
            showGenreStoryboard?.Begin();

            Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
            showFavoriteButtonStoryboard?.Begin();
        }
    }


    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is ArtistViewModel artistViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

            if (artistViewModel.IsFavorite)
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

    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void GroupListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ArtistsGroupCategoryViewModel group })
            ViewModel.ListenGroupCommand.Execute(group);
    }

    private void GridContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Grid root)
        {
            // Release storyboard holds so {x:Bind} values re-apply when the container is reused for another artist.
            StopStoryboard(root, "ShowGenreNameStoryboard");
            StopStoryboard(root, "ShowSubTitleStoryboard");
            StopStoryboard(root, "ShowFavoriteButtonStoryboard");
            StopStoryboard(root, "HideFavoriteButtonStoryboard");
            return;
        }

        if (args.Item is ArtistViewModel item && item.Picture == null)
            item.LoadPicture();
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

            if (grid is not null)
                grid.ItemsSource = null;

            if (ZoomoutCollectionGrid is not null)
                ZoomoutCollectionGrid.ItemsSource = null;

            if (groupedItemsViewSource is not null)
            {
                groupedItemsViewSource.Source = null;
                groupedItemsViewSource.IsSourceGrouped = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dispose in ArtistsPage");
        }

        _disposed = true;
    }
}