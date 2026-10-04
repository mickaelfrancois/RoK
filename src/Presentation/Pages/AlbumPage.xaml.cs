using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Track;

namespace Rok.Pages;

public sealed partial class AlbumPage : Page
{
    private const double NarrowHeaderThreshold = 800;
    private const double CompactHeaderThreshold = 560;

    /// <summary>Width reserved by the stats panel (250px) plus its paddings and grid margins.</summary>
    private const double StatsPanelReservedWidth = 300;

    private static readonly Lazy<double> TitleAndScoreColumnsWidth = new(() =>
        GetGridLengthResource("GridHeaderTracksTitleColumnWidth") + GetGridLengthResource("GridHeaderTracksScoreColumnWidth"));

    private static readonly Lazy<double> ArtistColumnWidth = new(() => GetGridLengthResource("GridHeaderTracksArtistColumnWidth"));

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
        base.OnNavigatedTo(e);

        if (e.Parameter is not AlbumOpenArgs options)
        {
            _logger.LogError("Navigation to AlbumPage without AlbumOpenArgs (received {ParameterType})", e.Parameter?.GetType().Name ?? "null");
            return;
        }

        try
        {
            await ViewModel.LoadDataAsync(options.AlbumId);
            UpdateStatsPanelVisibility(ActualWidth);
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
        ApplyHeaderLayout(e.NewSize.Width);
        UpdateStatsPanelVisibility(e.NewSize.Width);
    }

    private void AlbumPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyHeaderLayout(ActualWidth);
    }

    private void ApplyHeaderLayout(double width)
    {
        string state = width < CompactHeaderThreshold
            ? "CompactHeader"
            : width < NarrowHeaderThreshold ? "NarrowHeader" : "WideHeader";

        VisualStateManager.GoToState(this, state, true);
    }

    private void UpdateStatsPanelVisibility(double pageWidth)
    {
        double requiredTracksWidth = TitleAndScoreColumnsWidth.Value;

        if (ViewModel.Album.IsCompilation)
            requiredTracksWidth += ArtistColumnWidth.Value;

        statsPanel.Visibility = pageWidth >= requiredTracksWidth + StatsPanelReservedWidth
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static double GetGridLengthResource(string key)
    {
        return ((GridLength)Microsoft.UI.Xaml.Application.Current.Resources[key]).Value;
    }

    private void OnTrackTitleClick(object sender, RoutedEventArgs e)
    {
        // ICommand.Execute does not consult CanExecute: the guard has to be explicit here.
        if (sender is FrameworkElement { Tag: TrackViewModel track } && ViewModel.ListenCommand.CanExecute(track))
            ViewModel.ListenCommand.Execute(track);
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