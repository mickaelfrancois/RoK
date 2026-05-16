using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Insights;

namespace Rok.Pages;


public sealed partial class InsightsPage : Page
{
    public InsightsViewModel ViewModel { get; set; }
    private readonly ILogger<InsightsPage> _logger;


    public InsightsPage()
    {
        InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<InsightsPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<InsightsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            await ViewModel.LoadDataAsync();
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to InsightsPage failed");
        }
    }
}