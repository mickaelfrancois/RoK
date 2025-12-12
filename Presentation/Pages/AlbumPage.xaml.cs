using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Logic.ViewModels.Album;
using Rok.Logic.ViewModels.Albums;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Rok.Pages;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AlbumPage : Page
{
    public AlbumViewModel ViewModel { get; set; }

    public AlbumPage()
    {
        this.InitializeComponent();

        ViewModel = App.ServiceProvider.GetRequiredService<AlbumViewModel>();
        DataContext = ViewModel;
    }


    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AlbumOpenArgs options)
            throw new ArgumentNullException(nameof(options), "AlbumOpenArgs cannot be null");

        await ViewModel.LoadDataAsync(options.AlbumId);

        base.OnNavigatedTo(e);
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
