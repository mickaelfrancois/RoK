using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists;

namespace Rok.Pages;

public sealed partial class PlaylistsPage : Page
{
    public PlaylistsViewModel ViewModel { get; set; }
    private readonly ILogger<PlaylistsPage> _logger;


    public PlaylistsPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<PlaylistsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistsViewModel>();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            await ViewModel.LoadDataAsync(forceReload: false);
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to PlaylistsPage failed");
        }
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is PlaylistViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }
}