using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Insights;

namespace Rok.Pages;


public sealed partial class InsightsPage : Page
{
    public InsightsViewModel ViewModel { get; set; }


    public InsightsPage()
    {
        InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<InsightsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync();

        base.OnNavigatedTo(e);
    }
}
