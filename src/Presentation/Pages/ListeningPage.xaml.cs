using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Listening;
using Rok.ViewModels.Track;


namespace Rok.Pages;

public sealed partial class ListeningPage : Page, IDisposable
{
    public ListeningViewModel ViewModel { get; set; }


    public ListeningPage()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<ListeningViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        Dispose();
        base.OnNavigatingFrom(e);
    }

    private void SleepFlyout_Opening(object sender, object e)
    {
        ViewModel.RefreshSleepTime();
    }

    private void ListeningPage_Loaded(object sender, RoutedEventArgs e)
    {
        TrackViewModel? listeningTrack = ViewModel.Tracks.FirstOrDefault(track => track.Listening);

        if (listeningTrack != null)
            tracksList.ScrollIntoView(listeningTrack, ScrollIntoViewAlignment.Leading);
    }


    private void tracksList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
            return;

        if (args.ItemContainer?.ContentTemplateRoot is FrameworkElement root &&
            root.FindName("RowIndexText") is TextBlock tb)
        {
            tb.Text = (args.ItemIndex + 1).ToString() + ".";
        }
    }

    public void Dispose()
    {
        // ListeningViewModel is a singleton; releasing the x:Bind tracking lets the page be collected.
        Bindings.StopTracking();
        DataContext = null;
    }
}