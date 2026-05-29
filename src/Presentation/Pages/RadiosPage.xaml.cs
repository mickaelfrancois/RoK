using CleanArch.DevKit.Mediator;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Application.Dto;
using Rok.Dialogs;
using Rok.ViewModels.Radio;

namespace Rok.Pages;

public sealed partial class RadiosPage : Page
{
    public RadiosViewModel ViewModel { get; set; }
    private readonly ILogger<RadiosPage> _logger;
    private readonly ResourceLoader _resourceLoader;

    public RadiosPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<RadiosPage>>();
        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        ViewModel = App.ServiceProvider.GetRequiredService<RadiosViewModel>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            await ViewModel.LoadAsync();
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to RadiosPage failed");
        }
    }

    private async void OnAddClick(object sender, RoutedEventArgs e)
    {
        AddRadioStationDialog dialog = new(App.ServiceProvider.GetRequiredService<IMediator>())
        {
            XamlRoot = XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Saved)
            await ViewModel.LoadAsync();
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        SearchRadioStationsDialog dialog = new(
            App.ServiceProvider.GetRequiredService<SearchRadioStationsViewModel>())
        {
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
        await ViewModel.LoadAsync();
    }

    private async void OnPlayUrlClick(object sender, RoutedEventArgs e)
    {
        PlayRadioUrlDialog dialog = new(App.ServiceProvider.GetRequiredService<IMediator>())
        {
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RadioStationDto station)
            _ = ViewModel.PlayCommand.ExecuteAsync(station);
    }

    private void OnTileNameClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RadioStationDto station })
            _ = ViewModel.PlayCommand.ExecuteAsync(station);
    }

    private async void OnDeleteMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: RadioStationDto station })
            return;

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationTitle"),
            Content = string.Format(_resourceLoader.GetString("radiosDeleteConfirmation"), station.Name),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ViewModel.DeleteCommand.ExecuteAsync(station);
    }
}