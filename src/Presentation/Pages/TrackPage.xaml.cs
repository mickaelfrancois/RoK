using System.IO;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album.Services;
using Rok.ViewModels.Track;


namespace Rok.Pages;

public sealed partial class TrackPage : Page
{
    private const double NarrowHeaderThreshold = 800;

    public TrackViewModel ViewModel { get; set; } = null!;
    private readonly ILogger<TrackPage> _logger;

    public TrackPage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<TrackPage>>();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not TrackOpenArgs options)
        {
            _logger.LogError("Navigation to TrackPage without TrackOpenArgs (received {ParameterType})", e.Parameter?.GetType().Name ?? "null");
            return;
        }

        try
        {
            ViewModel = App.ServiceProvider.GetRequiredService<TrackViewModel>();
            DataContext = ViewModel;

            await ViewModel.LoadDataAsync(options.TrackId);
            LoadCover();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to TrackPage failed");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        // The view model subscribes to score updates: without this every visit would keep one alive.
        ViewModel?.Dispose();
        base.OnNavigatedFrom(e);
    }

    private void LoadCover()
    {
        string? albumFolder = Path.GetDirectoryName(ViewModel.Track.MusicFile);

        if (string.IsNullOrEmpty(albumFolder))
            return;

        AlbumPictureService pictureService = App.ServiceProvider.GetRequiredService<AlbumPictureService>();
        BitmapImage? cover = pictureService.LoadPicture(albumFolder);

        if (cover is not null)
            coverPicture.Cover = cover;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyHeaderLayout(e.NewSize.Width);
    }

    private void TrackPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyHeaderLayout(ActualWidth);
    }

    private void ApplyHeaderLayout(double width)
    {
        string state = width < NarrowHeaderThreshold ? "NarrowHeader" : "WideHeader";

        VisualStateManager.GoToState(this, state, true);
    }
}