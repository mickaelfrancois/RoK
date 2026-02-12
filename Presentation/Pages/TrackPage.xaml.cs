using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Logic.ViewModels.Tracks;
using Rok.ViewModels.Track;


namespace Rok.Pages;

public sealed partial class TrackPage : Page
{
    public TrackViewModel ViewModel { get; set; } = null!;

    public TrackPage()
    {
        this.InitializeComponent();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not TrackOpenArgs options)
            throw new ArgumentException("Navigation parameters must be type of TrackOpenArgs", nameof(e));

        ViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
        DataContext = ViewModel;

        await ViewModel.LoadDataAsync(options.TrackId);

        base.OnNavigatedTo(e);
    }
}
