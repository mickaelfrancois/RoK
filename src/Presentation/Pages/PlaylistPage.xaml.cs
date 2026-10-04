using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Playlist;

namespace Rok.Pages;

public sealed partial class PlaylistPage : Page
{
    private const double NarrowHeaderThreshold = 800;

    public PlaylistViewModel ViewModel { get; set; }
    private readonly ResourceLoader _resourceLoader;
    private readonly ILogger<PlaylistPage> _logger;

    public PlaylistPage()
    {
        InitializeComponent();

        _resourceLoader = App.ServiceProvider.GetRequiredService<ResourceLoader>();
        _logger = App.ServiceProvider.GetRequiredService<ILogger<PlaylistPage>>();
        ViewModel = App.ServiceProvider.GetRequiredService<PlaylistViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not PlaylistOpenArgs options)
        {
            _logger.LogError("Navigation to PlaylistPage without PlaylistOpenArgs (received {ParameterType})", e.Parameter?.GetType().Name ?? "null");
            return;
        }

        try
        {
            if (options.PlaylistId.HasValue)
                await ViewModel.LoadDataAsync(options.PlaylistId.Value);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to PlaylistPage failed");
        }
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyHeaderLayout(e.NewSize.Width);
    }

    private void PlaylistPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyHeaderLayout(ActualWidth);
    }

    private void ApplyHeaderLayout(double width)
    {
        string state = width < NarrowHeaderThreshold ? "NarrowHeader" : "WideHeader";

        VisualStateManager.GoToState(this, state, true);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = _resourceLoader.GetString("DeleteConfirmationTitle"),
            Content = _resourceLoader.GetString("DeletePlaylistConfirmation"),
            PrimaryButtonText = _resourceLoader.GetString("YesButton"),
            CloseButtonText = _resourceLoader.GetString("CancelButton"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && ViewModel.DeleteCommand.CanExecute(null))
            await ViewModel.DeleteCommand.ExecuteAsync(null);
    }

    private void tracksList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
        {
            ViewModel.MoveTrackCommand.Execute(null);
        }
    }

    private void TracksListContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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