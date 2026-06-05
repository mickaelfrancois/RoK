using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;

namespace Rok.Pages;

public sealed partial class AlbumPage : Page
{
    /// <summary>Width reserved by the stats panel (250px) plus its paddings and grid margins.</summary>
    private const double StatsPanelReservedWidth = 300;

    public AlbumViewModel ViewModel { get; set; }
    private readonly ILogger<AlbumPage> _logger;

    public AlbumPage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<AlbumPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
        DataContext = ViewModel;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AlbumOpenArgs options)
            throw new ArgumentNullException(nameof(options), "AlbumOpenArgs cannot be null");

        try
        {
            await ViewModel.LoadDataAsync(options.AlbumId);
            UpdateStatsPanelVisibility(ActualWidth);
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to AlbumPage failed");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.OnNavigatedFrom();
        base.OnNavigatedFrom(e);
    }


    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateStatsPanelVisibility(e.NewSize.Width);
    }

    private void UpdateStatsPanelVisibility(double pageWidth)
    {
        double requiredTracksWidth = GetGridLengthResource("GridHeaderTracksTitleColumnWidth")
                                   + GetGridLengthResource("GridHeaderTracksScoreColumnWidth");

        if (ViewModel.Album.IsCompilation)
            requiredTracksWidth += GetGridLengthResource("GridHeaderTracksArtistColumnWidth");

        statsPanel.Visibility = pageWidth >= requiredTracksWidth + StatsPanelReservedWidth
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static double GetGridLengthResource(string key)
    {
        return ((GridLength)Microsoft.UI.Xaml.Application.Current.Resources[key]).Value;
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
}