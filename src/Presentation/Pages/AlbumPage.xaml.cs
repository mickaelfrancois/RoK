using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;

namespace Rok.Pages;

public sealed partial class AlbumPage : Page
{
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