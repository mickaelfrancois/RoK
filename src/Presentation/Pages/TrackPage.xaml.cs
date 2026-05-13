using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Track;


namespace Rok.Pages;

public sealed partial class TrackPage : Page
{
    public TrackViewModel ViewModel { get; set; } = null!;
    private readonly ILogger<TrackPage> _logger;

    public TrackPage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<TrackPage>>();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not TrackOpenArgs options)
            throw new ArgumentException("Navigation parameters must be type of TrackOpenArgs", nameof(e));

        try
        {
            ViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
            DataContext = ViewModel;

            await ViewModel.LoadDataAsync(options.TrackId);
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to TrackPage failed");
        }
    }
}
