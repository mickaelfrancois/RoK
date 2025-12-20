using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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


    public void Dispose()
    {
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is PlaylistViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }
}
