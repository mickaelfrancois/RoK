using Microsoft.UI.Xaml.Controls;
using Rok.Services.Diagnostics;
using Rok.ViewModels.Listening;
using Rok.ViewModels.Track;


namespace Rok.Pages;

public sealed partial class ListeningPage : Page, IDisposable
{
    public ListeningViewModel ViewModel { get; set; }

    private const double NarrowHeaderThreshold = 800;

    private readonly ResourceLoader _resourceLoader;
    private readonly ICrashBreadcrumbs _breadcrumbs;
    private readonly Func<int, Task<bool>> _confirmQueueRemoval;


    public ListeningPage()
    {
        this.InitializeComponent();

        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        _breadcrumbs = App.ServiceProvider.GetRequiredService<ICrashBreadcrumbs>();
        ViewModel = App.ServiceProvider.GetRequiredService<ListeningViewModel>();
        _confirmQueueRemoval = ConfirmQueueRemovalAsync;
        ViewModel.RemovalConfirmationRequested = _confirmQueueRemoval;
        DataContext = ViewModel;

        Unloaded += ListeningPage_Unloaded;
    }

    private void ListeningPage_Unloaded(object sender, RoutedEventArgs e)
    {
        // The page has left the visual tree: no container is realized any more, so the list can
        // be detached from the singleton queue without feeding a null item to the bindings.
        _breadcrumbs.Add("listening.page", "unloaded");
        Dispose();
    }

    private void SleepFlyout_Opening(object sender, object e)
    {
        ViewModel.RefreshSleepTime();
    }

    private async Task<bool> ConfirmQueueRemovalAsync(int trackCount)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("RemoveFromQueueConfirmationTitle"),
            Content = string.Format(_resourceLoader.GetString("RemoveFromQueueConfirmation"), trackCount),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();

        return result == ContentDialogResult.Primary;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyHeaderLayout(e.NewSize.Width);
    }

    private void ApplyHeaderLayout(double width)
    {
        string state = width < NarrowHeaderThreshold ? "NarrowHeader" : "WideHeader";

        VisualStateManager.GoToState(this, state, true);
    }

    private void ListeningPage_Loaded(object sender, RoutedEventArgs e)
    {
        _breadcrumbs.Add("listening.page", "loaded");
        ApplyHeaderLayout(ActualWidth);
        tracksList.ItemsSource ??= ViewModel.Tracks;

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
        // ListeningViewModel is a singleton: detaching the list and the x:Bind tracking lets the page be
        // collected. The confirmation delegate is cleared only if a newer page has not taken it over.
        tracksList.ItemsSource = null;

        if (ViewModel.RemovalConfirmationRequested == _confirmQueueRemoval)
            ViewModel.RemovalConfirmationRequested = null;

        Bindings.StopTracking();
        DataContext = null;
    }
}