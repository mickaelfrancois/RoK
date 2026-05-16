using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Playlist;
using Rok.ViewModels.Playlists;

namespace Rok.Pages;

public sealed partial class PlaylistsPage : Page, IDisposable
{
    public PlaylistsViewModel ViewModel { get; set; }
    private readonly ILogger<PlaylistsPage> _logger;


    public PlaylistsPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<PlaylistsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistsViewModel>();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
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
            _logger.LogError(ex, "Navigation to PlaylistsPage failed");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistsViewModel.IsGridView))
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        VisualStateManager.GoToState(this, ViewModel.IsGridView ? "GridViewState" : "ListViewState", true);
    }

    public void Dispose()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is PlaylistViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }
}