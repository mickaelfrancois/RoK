using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.Logic.ViewModels.Tracks;


namespace Rok.Pages;

public sealed partial class TracksPage : Page, IDisposable
{
    public TracksViewModel ViewModel { get; set; }

    private readonly TracksFilterMenuBuilder _filterMenuBuilder = new();


    public TracksPage()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<TracksViewModel>();
        DataContext = ViewModel;

        Loaded += Page_Loaded;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);

        base.OnNavigatedTo(e);
    }


    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(tracksList);
        ViewModel.SaveState();

        base.OnNavigatingFrom(e);
    }


    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ScrollStateHelper.RestoreScrollOffset(tracksList);
    }


    private void FilterFlyout_Opened(object sender, object e)
    {
        _filterMenuBuilder.PopulateFilterMenu(filterMenu, ViewModel);
    }


    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }


    public void Dispose()
    {
        Loaded -= Page_Loaded;
    }
}
