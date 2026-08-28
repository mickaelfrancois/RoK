using System.Collections;
using Microsoft.UI.Xaml.Controls;

namespace Rok.Commons;

/// <summary>
/// Wires the items source of a grouped list, and rewires it only when the wiring mode changes.
/// Every redundant reassignment is a window where the framework can process a container whose item
/// has already left the source, which throws inside the generated x:Bind bindings.
/// </summary>
internal sealed class GroupedItemsSourceBinder
{
    private static readonly object[] EmptySource = [];

    private readonly ListViewBase _zoomedInView;
    private readonly ListViewBase _zoomedOutView;
    private readonly CollectionViewSource _viewSource;
    private readonly IEnumerable _groupedSource;
    private readonly ILogger _logger;

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
    /// Applies the wiring the current state requires. Does nothing when the list is already wired
    /// the right way.
    /// </summary>
    /// <param name="isGroupingEnabled">Whether the view model currently groups its items.</param>
    /// <param name="firstGroupItems">The items of the first group, or <c>null</c> when there is no group.</param>
    public void Apply(bool isGroupingEnabled, IList? firstGroupItems)
    {
        GroupedItemsSourceMode next = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled, firstGroupItems is not null, _mode);

        if (!GroupedItemsSourcePlanner.RequiresRewire(_mode, next))
            return;

        if (next == GroupedItemsSourceMode.Grouped)
            WireGrouped();
        else
            WireFlat(firstGroupItems);

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

        _mode = GroupedItemsSourceMode.None;
    }


    private void WireGrouped()
    {
        _viewSource.IsSourceGrouped = true;
        _viewSource.Source = _groupedSource;

        _zoomedInView.ItemsSource = _viewSource.View;
        _zoomedOutView.ItemsSource = _viewSource.View.CollectionGroups;
    }


    private void WireFlat(IList? firstGroupItems)
    {
        bool leavingGroupedMode = _mode == GroupedItemsSourceMode.Grouped;

        // The zoomed out view reads the grouped view, so it must let go before that view dies.
        if (leavingGroupedMode)
            _zoomedOutView.ItemsSource = null;

        // Assign the new source directly: going through null would recycle every container while
        // the page is still on screen.
        _zoomedInView.ItemsSource = firstGroupItems ?? EmptySource;

        if (leavingGroupedMode)
        {
            _viewSource.Source = null;
            _viewSource.IsSourceGrouped = false;
        }
    }
}