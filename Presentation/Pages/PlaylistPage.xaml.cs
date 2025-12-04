using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Logic.ViewModels.Playlist;
using Rok.Logic.ViewModels.Playlists;

namespace Rok.Pages;

public sealed partial class PlaylistPage : Page
{
    public PlaylistViewModel ViewModel { get; set; }
    private readonly ResourceLoader _resourceLoader;

    public PlaylistPage()
    {
        InitializeComponent();

        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not PlaylistOpenArgs options)
            throw new ArgumentNullException(nameof(e), "PlaylistOpenArgs cannot be null");

        if (options.PlaylistId.HasValue)
            await ViewModel.LoadDataAsync(options.PlaylistId.Value);

        base.OnNavigatedTo(e);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationTitle"),
            Content = _resourceLoader.GetString("DeletePlaylistConfirmation"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && ViewModel.DeleteCommand.CanExecute(null))
            ViewModel.DeleteCommand.Execute(null);
    }
}
