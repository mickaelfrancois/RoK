using System.ComponentModel;
using System.Threading;
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
    private bool _pageLoaded;

    private readonly AnimatedNumberHelper _countAnimation;
    private readonly AnimatedNumberHelper _durationAnimation;

    private readonly GroupedItemsSourceBinder _binder;


    public TracksPage()
    {
        InitializeComponent();

        _countAnimation = new AnimatedNumberHelper(t => trackCountRun.Text = t);
        _durationAnimation = new AnimatedNumberHelper(t => trackDurationRun.Text = t);

        _logger = App.ServiceProvider.GetRequiredService<ILogger<TracksPage>>();

        ViewModel = App.ServiceProvider.GetRequiredService<TracksViewModel>();

        _binder = new GroupedItemsSourceBinder(tracksList, ZoomoutCollectionGrid, groupedItemsViewSource, ViewModel.GroupedItems, _logger);

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.GroupedItems.CollectionChanged += GroupedItems_CollectionChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            await ViewModel.LoadDataAsync(forceReload: false);
            base.OnNavigatedTo(e);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation to TracksPage failed");
        }
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        ScrollStateHelper.SaveScrollOffset(tracksList);
        ViewModel.SaveState();

        _pageLoaded = false;
        Dispose();

        base.OnNavigatingFrom(e);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _pageLoaded = true;
        UpdateItemsSource();
        ScrollStateHelper.RestoreScrollOffset(tracksList);
        _countAnimation.AnimateTo(ViewModel.Count);
        _durationAnimation.AnimateTo(ViewModel.DurationText);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // The page has left the visual tree: no container is realized any more, so releasing the
        // sources here can no longer feed a null item to the generated bindings.
        _binder.Release();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Count))
        {
            if (_pageLoaded)
                _countAnimation.AnimateTo(ViewModel.Count);
            return;
        }

        if (e.PropertyName == nameof(ViewModel.DurationText))
        {
            if (_pageLoaded)
                _durationAnimation.AnimateTo(ViewModel.DurationText);
            return;
        }

        if (e.PropertyName == nameof(ViewModel.IsGroupingEnabled))
        {
            // The flag is published before the data, so the rewiring happens on the collection
            // Reset that follows. Only the zoom state is handled here.
            if (!ViewModel.IsGroupingEnabled && !tracksListZoom.IsZoomedInViewActive)
                tracksListZoom.IsZoomedInViewActive = true;
        }
    }

    private void GroupedItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel.GroupedItems.Count == 0 && !tracksListZoom.IsZoomedInViewActive)
            tracksListZoom.IsZoomedInViewActive = true;

        UpdateItemsSource();
    }

    private void UpdateItemsSource()
    {
        _binder.Apply(ViewModel.IsGroupingEnabled, ViewModel.GroupedItems.Count > 0 ? ViewModel.GroupedItems[0].Items : null);
    }

    private void FilterFlyout_Opened(object sender, object e)
    {
        _filterMenuBuilder.PopulateFilterMenu(filterMenu, ViewModel);
    }

    private void GroupButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void GroupListenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TracksGroupCategoryViewModel group })
            ViewModel.ListenGroupCommand.Execute(group);
    }

    private void GroupByFlyout_Opened(object sender, object e)
    {
        _groupByMenuBuilder.PopulateGroupByMenu(groupByMenu, ViewModel);
    }

    public void Dispose()
    {
        if (!this.DispatcherQueue.HasThreadAccess)
        {
            this.DispatcherQueue.TryEnqueue(() => Dispose());
            return;
        }

        if (Interlocked.Exchange(ref _disposed, true))
            return;

        try
        {
            Loaded -= Page_Loaded;

            _countAnimation.Dispose();
            _durationAnimation.Dispose();

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.GroupedItems.CollectionChanged -= GroupedItems_CollectionChanged;
            }

            // The sources are released in Page_Unloaded, once the page has left the visual tree.
            // Detaching them here would recycle containers while the page is still on screen.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Dispose in TracksPage");
        }
    }
}