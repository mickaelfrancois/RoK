using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.Logic.ViewModels.Albums;

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
        ScrollStateHelper.RestoreScrollOffset(grid);
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
    }
}
