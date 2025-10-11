using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.Logic.ViewModels.Playlists;

namespace Rok.Pages;

public sealed partial class PlaylistsPage : Page, IDisposable
{
    public PlaylistsViewModel ViewModel { get; set; }


    public PlaylistsPage()
    {
        InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistsViewModel>();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);

        base.OnNavigatedTo(e);
    }


    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(grid);

        base.OnNavigatingFrom(e);
    }


    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ScrollStateHelper.RestoreScrollOffset(grid);
    }

    public void Dispose()
    {
        Loaded -= Page_Loaded;
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is PlaylistViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }
}
