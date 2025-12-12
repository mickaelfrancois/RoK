using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.Logic.ViewModels.Albums;
using System.ComponentModel;

namespace Rok.Pages;

public sealed partial class AlbumsPage : Page, IDisposable
{
    public AlbumsViewModel ViewModel { get; set; }

    private readonly AlbumsFilterMenuBuilder _filterMenuBuilder = new();
    private readonly AlbumsGroupByMenuBuilder _groupByMenuBuilder = new();

    public AlbumsPage()
    {
        this.InitializeComponent();


        ViewModel = App.ServiceProvider.GetRequiredService<AlbumsViewModel>();
        DataContext = ViewModel;

        Loaded += Page_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);
        base.OnNavigatedTo(e);
    }


    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(grid);
        ViewModel.SaveState();
        base.OnNavigatingFrom(e);
    }


    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateItemsSource();
        ScrollStateHelper.RestoreScrollOffset(grid);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsGroupingEnabled))
        {
            if (!ViewModel.IsGroupingEnabled && !GridZoom.IsZoomedInViewActive)
            {
                GridZoom.IsZoomedInViewActive = true;
            }

            UpdateItemsSource();
        }
    }

    private void UpdateItemsSource()
    {
        if (ViewModel.GroupedItems.Count == 0)
            return;

        if (ViewModel.IsGroupingEnabled)
        {
            groupedItemsViewSource.IsSourceGrouped = true;
            groupedItemsViewSource.Source = ViewModel.GroupedItems;

            grid.ItemsSource = groupedItemsViewSource.View;
            ZoomoutCollectionGrid.ItemsSource = groupedItemsViewSource.View.CollectionGroups;
        }
        else
        {
            groupedItemsViewSource.IsSourceGrouped = false;
            grid.ItemsSource = ViewModel.GroupedItems[0].Items;
            ZoomoutCollectionGrid.ItemsSource = null;
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
        if (gridItem != null)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();
        }
    }


    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();
        }
    }


    public void Dispose()
    {
        Loaded -= Page_Loaded;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }
}
