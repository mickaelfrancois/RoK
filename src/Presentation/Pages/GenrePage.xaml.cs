using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Genre;


namespace Rok.Pages;

public sealed partial class GenrePage : Page
{
    private const double NarrowHeaderThreshold = 800;

    public GenreViewModel ViewModel { get; set; }
    private readonly ILogger<GenrePage> _logger;

    public GenrePage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<GenrePage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<GenreViewModel>();
        DataContext = ViewModel;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not GenreOpenArgs options)
        {
            _logger.LogError("Navigation to GenrePage without GenreOpenArgs (received {ParameterType})", e.Parameter?.GetType().Name ?? "null");
            return;
        }

        try
        {
            await ViewModel.LoadDataAsync(options.GenreId);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to GenrePage failed");
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
    }

    private void GenrePage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyHeaderLayout(ActualWidth);
    }

    private void ApplyHeaderLayout(double width)
    {
        string state = width < NarrowHeaderThreshold ? "NarrowHeader" : "WideHeader";

        VisualStateManager.GoToState(this, state, true);
    }

    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue && args.ItemContainer?.ContentTemplateRoot is Grid root)
        {
            // Release storyboard holds so {x:Bind} values re-apply when the container is reused for another album.
            StopStoryboard(root, "ShowArtistNameStoryboard");
            StopStoryboard(root, "ShowSubTitleStoryboard");
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
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel)
        {
            Storyboard? showArtistStoryboard = gridItem.Resources["ShowArtistNameStoryboard"] as Storyboard;
            showArtistStoryboard?.Begin();

            Storyboard? showFavoriteButtonStoryboard = gridItem.Resources["ShowFavoriteButtonStoryboard"] as Storyboard;
            showFavoriteButtonStoryboard?.Begin();
        }
    }

    private void gridBottom_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        Grid? gridItem = sender as Grid;
        if (gridItem != null && gridItem.DataContext is AlbumViewModel albumViewModel)
        {
            Storyboard? showSubTitleStoryboard = gridItem.Resources["ShowSubTitleStoryboard"] as Storyboard;
            showSubTitleStoryboard?.Begin();

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
}