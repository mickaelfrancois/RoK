using System.ComponentModel;
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
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);
        UpdateVisualState();

        base.OnNavigatedTo(e);
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
