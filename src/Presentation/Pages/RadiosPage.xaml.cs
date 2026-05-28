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

    public RadiosPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<RadiosPage>>();
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
}
