using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Rok.Commons;
using Rok.ViewModels.Tracks;

namespace Rok.Pages;

public sealed partial class TracksPage : Page, IDisposable
{
    private readonly ILogger<TracksPage> _logger;
    public TracksViewModel ViewModel { get; set; }

    private readonly TracksFilterMenuBuilder _filterMenuBuilder = new();
    private readonly TracksGroupByMenuBuilder _groupByMenuBuilder = new();

    private bool _disposed;

    public TracksPage()
    {
        this.InitializeComponent();

        _logger = App.ServiceProvider.GetRequiredService<ILogger<TracksPage>>();

        ViewModel = App.ServiceProvider.GetRequiredService<TracksViewModel>();
        DataContext = ViewModel;

        Loaded += Page_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        await ViewModel.LoadDataAsync(forceReload: false);
        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(tracksList);
        ViewModel.SaveState();

        // Cleanup bindings and handlers to avoid keeping generated binding tracking objects alive
        Dispose();

        base.OnNavigatingFrom(e);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateItemsSource();
        ScrollStateHelper.RestoreScrollOffset(tracksList);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsGroupingEnabled))
        {
            if (!ViewModel.IsGroupingEnabled && !tracksListZoom.IsZoomedInViewActive)
            {
                tracksListZoom.IsZoomedInViewActive = true;
            }
        }
        UpdateItemsSource();
    }

    private void UpdateItemsSource()
    {
        if (ViewModel.GroupedItems.Count == 0)
            return;

        if (ViewModel.IsGroupingEnabled)
        {
            groupedItemsViewSource.IsSourceGrouped = true;
            groupedItemsViewSource.Source = ViewModel.GroupedItems;

            tracksList.ItemsSource = groupedItemsViewSource.View;
            tracksZoomoutCollectionList.ItemsSource = groupedItemsViewSource.View.CollectionGroups;
        }
        else
        {
            groupedItemsViewSource.IsSourceGrouped = false;
            tracksList.ItemsSource = ViewModel.GroupedItems[0].Items;
            tracksZoomoutCollectionList.ItemsSource = null;
        }
    }

    private void FilterFlyout_Opened(object sender, object e)
    {
        _filterMenuBuilder.PopulateFilterMenu(filterMenu, ViewModel);
    }

    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void GroupByFlyout_Opened(object sender, object e)
    {
        _groupByMenuBuilder.PopulateGroupByMenu(groupByMenu, ViewModel);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            Loaded -= Page_Loaded;

            if (ViewModel != null)
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

            if (tracksList is not null)
                tracksList.ItemsSource = null;

            if (tracksZoomoutCollectionList is not null)
                tracksZoomoutCollectionList.ItemsSource = null;

            if (groupedItemsViewSource is not null)
            {
                groupedItemsViewSource.Source = null;
                groupedItemsViewSource.IsSourceGrouped = false;
            }

            DataContext = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dispose in TracksPage");
        }

        _disposed = true;
    }
}