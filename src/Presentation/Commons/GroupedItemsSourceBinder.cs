using System.Collections;
using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

/// <summary>
/// Wires the items source of a grouped list, and rewires it only when the wiring mode changes.
/// In flat mode the list reads a collection this binder owns, so a filtering pass only refills it
/// with a single Reset. Every reassignment of the items source is a window where the framework can
/// process a container whose item has already left the source, which throws inside the generated
/// x:Bind bindings.
/// </summary>
internal sealed class GroupedItemsSourceBinder
{
    private readonly ListViewBase _zoomedInView;
    private readonly ListViewBase _zoomedOutView;
    private readonly CollectionViewSource _viewSource;
    private readonly IEnumerable _groupedSource;
    private readonly ILogger _logger;

    // The flat mode reads this collection, never the list the view model rebuilds on every
    // filtering pass. Its instance outlives every filter, so the items source stays put.
    private readonly RangeObservableCollection<object> _flatItems = [];

    private GroupedItemsSourceMode _mode = GroupedItemsSourceMode.None;

    public GroupedItemsSourceBinder(ListViewBase zoomedInView, ListViewBase zoomedOutView, CollectionViewSource viewSource, IEnumerable groupedSource, ILogger logger)
    {
        _zoomedInView = zoomedInView;
        _zoomedOutView = zoomedOutView;
        _viewSource = viewSource;
        _groupedSource = groupedSource;
        _logger = logger;
    }


    /// <summary>
    /// Applies the wiring the current state requires. Refreshes the flat content first, then
    /// rewires the list only when the mode changes.
    /// </summary>
    /// <param name="isGroupingEnabled">Whether the view model currently groups its items.</param>
    /// <param name="firstGroupItems">The items of the first group, or <c>null</c> when there is no group.</param>
    public void Apply(bool isGroupingEnabled, IList? firstGroupItems)
    {
        GroupedItemsSourceMode next = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled, firstGroupItems is not null, _mode);

        // Refresh before the early return below: staying flat no longer rewires anything, so this
        // single Reset is what a filtering pass shows. It also fills the collection before it
        // becomes the source, so the list never reads an empty intermediate state.
        if (GroupedItemsSourcePlanner.RequiresContentRefresh(next))
            _flatItems.InitWithAddRange(firstGroupItems?.Cast<object>() ?? []);

        if (!GroupedItemsSourcePlanner.RequiresRewire(_mode, next))
            return;

        if (next == GroupedItemsSourceMode.Grouped)
            WireGrouped();
        else
            WireFlat();

        _mode = next;

        _logger.LogDebug("Grouped items source rewired to {Mode}.", next);
    }


    /// <summary>
    /// Detaches every source. Idempotent, and safe to call after the page left the visual tree.
    /// </summary>
    public void Release()
    {
        _zoomedOutView.ItemsSource = null;
        _zoomedInView.ItemsSource = null;
        _viewSource.Source = null;
        _viewSource.IsSourceGrouped = false;

        // Cleared last: no live list reads the collection any more, so this Reset reaches nobody.
        _flatItems.Clear();

        _mode = GroupedItemsSourceMode.None;
    }


    private void WireGrouped()
    {
        _viewSource.IsSourceGrouped = true;
        _viewSource.Source = _groupedSource;

        _zoomedInView.ItemsSource = _viewSource.View;
        _zoomedOutView.ItemsSource = _viewSource.View.CollectionGroups;
    }


    private void WireFlat()
    {
        bool leavingGroupedMode = _mode == GroupedItemsSourceMode.Grouped;

        // The zoomed out view reads the grouped view, so it must let go before that view dies.
        if (leavingGroupedMode)
            _zoomedOutView.ItemsSource = null;

        // Assign the stable collection directly: going through null would recycle every container
        // while the page is still on screen.
        _zoomedInView.ItemsSource = _flatItems;

        if (leavingGroupedMode)
        {
            _viewSource.Source = null;
            _viewSource.IsSourceGrouped = false;
        }
    }
}