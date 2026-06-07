using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;

namespace Rok.Pages;

public sealed partial class ArtistPage : Page
{
    /// <summary>Width reserved by the stats panel (250px) plus its paddings and grid margins.</summary>
    private const double StatsPanelReservedWidth = 300;

    public ArtistViewModel ViewModel { get; set; } = null!;
    private readonly ILogger<ArtistPage> _logger;

    public ArtistPage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<ArtistPage>>();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not ArtistOpenArgs options)
            throw new ArgumentException("Navigation parameters must be type of ArtistOpenArgs", nameof(e));

        try
        {
            ViewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
            DataContext = ViewModel;

            await ViewModel.LoadDataAsync(options.ArtistId);
            UpdateStatsPanelVisibility(ActualWidth);
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to ArtistPage failed");
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
        // The tracks tab is the widest pivot content: title, album and score fixed columns.
        double requiredTracksWidth = GetGridLengthResource("GridHeaderTracksTitleColumnWidth")
                                   + GetGridLengthResource("GridHeaderTracksAlbumColumnWidth")
                                   + GetGridLengthResource("GridHeaderTracksScoreColumnWidth");

        statsPanel.Visibility = pageWidth >= requiredTracksWidth + StatsPanelReservedWidth
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static double GetGridLengthResource(string key)
    {
        return ((GridLength)Microsoft.UI.Xaml.Application.Current.Resources[key]).Value;
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Grid root)
        {
            // Release storyboard holds so {x:Bind} values re-apply when the container is reused for another album.
            StopStoryboard(root, "ShowFavoriteButtonStoryboard");
            StopStoryboard(root, "HideFavoriteButtonStoryboard");
            return;
        }

        if (args.Item is AlbumViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
    }

    private void gridBottom_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid gridItem && gridItem.DataContext is AlbumViewModel)
        {
            Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
            showFavoriteButtonStoryboard?.Begin();
        }
    }

    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid gridItem && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            if (albumViewModel.IsFavorite)
            {
                // Release the animated values so the {x:Bind} on Opacity stays authoritative for favorites.
                StopStoryboard(gridItem, "ShowFavoriteButtonStoryboard");
                StopStoryboard(gridItem, "HideFavoriteButtonStoryboard");
            }
            else
            {
                Storyboard? hideFavoriteButtonStoryboard = gridItem.Resources["HideFavoriteButtonStoryboard"] as Storyboard;
                hideFavoriteButtonStoryboard?.Begin();
            }
        }
    }

    private static void StopStoryboard(Grid root, string key)
    {
        if (root.Resources.TryGetValue(key, out object? value) && value is Storyboard storyboard)
            storyboard.Stop();
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