using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.ViewModels.Album;
using Rok.ViewModels.Artist;

namespace Rok.Pages;

public sealed partial class ArtistPage : Page
{
    public ArtistViewModel ViewModel { get; set; } = null!;

    public ArtistPage()
    {
        this.InitializeComponent();
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not ArtistOpenArgs options)
            throw new ArgumentException("Navigation parameters must be type of ArtistOpenArgs", nameof(e));

        ViewModel = App.ServiceProvider.GetRequiredService<ArtistViewModel>();
        DataContext = ViewModel;

        await ViewModel.LoadDataAsync(options.ArtistId);

        base.OnNavigatedTo(e);
    }


    private void grid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is AlbumViewModel item && item.Picture == null)
        {
            item.LoadPicture();
        }
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
