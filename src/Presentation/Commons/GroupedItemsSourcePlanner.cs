namespace Rok.Commons;

/// <summary>
/// Describes how the items source of a grouped list is currently wired.
/// </summary>
public enum GroupedItemsSourceMode
{
    /// <summary>No source is wired yet.</summary>
    None,

    /// <summary>The list is wired to the grouped view of a <c>CollectionViewSource</c>.</summary>
    Grouped,

    /// <summary>The list is wired directly to a flat item list.</summary>
    Flat
}

/// <summary>
/// Decides how a grouped list must be wired, and whether that wiring has to be rebuilt.
/// Holds no UI dependency so the rule stays testable.
/// </summary>
public static class GroupedItemsSourcePlanner
{
    /// <summary>
    /// Resolves the wiring mode to apply.
    /// </summary>
    /// <param name="isGroupingEnabled">Whether the view model currently groups its items.</param>
    /// <param name="hasGroups">Whether at least one group is available.</param>
    /// <param name="current">The mode currently wired on the list.</param>
    /// <returns>The mode the list must end up in.</returns>
    public static GroupedItemsSourceMode ResolveMode(bool isGroupingEnabled, bool hasGroups, GroupedItemsSourceMode current)
    {
        if (!hasGroups)
            return current == GroupedItemsSourceMode.Grouped ? GroupedItemsSourceMode.Grouped : GroupedItemsSourceMode.Flat;

        return isGroupingEnabled ? GroupedItemsSourceMode.Grouped : GroupedItemsSourceMode.Flat;
    }

    /// <summary>
    /// Tells whether moving from <paramref name="current"/> to <paramref name="next"/> requires
    /// rewiring the items source.
    /// </summary>
    /// <param name="current">The mode currently wired on the list.</param>
    /// <param name="next">The mode resolved by <see cref="ResolveMode"/>.</param>
    /// <returns><c>true</c> when the items source must be reassigned.</returns>
    public static bool RequiresRewire(GroupedItemsSourceMode current, GroupedItemsSourceMode next)
    {
        // A mode that stays itself never needs a rewire: both modes read a collection whose
        // instance never changes. Grouped goes through the CollectionViewSource, flat through the
        // stable collection the binder owns, and both rebuild their content on a Reset.
        return current != next;
    }

    /// <summary>
    /// Tells whether the flat source must be refilled to show <paramref name="next"/>.
    /// </summary>
    /// <param name="next">The mode resolved by <see cref="ResolveMode"/>.</param>
    /// <returns><c>true</c> when the stable flat collection must be refilled.</returns>
    public static bool RequiresContentRefresh(GroupedItemsSourceMode next)
    {
        // Grouped reads the view model collection directly, so it needs no copy. Flat reads the
        // collection the binder owns, which a new filtering pass leaves stale.
        return next == GroupedItemsSourceMode.Flat;
    }
}