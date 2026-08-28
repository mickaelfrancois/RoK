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
        if (current != next)
            return true;

        // Staying grouped costs nothing: the CollectionViewSource observes a stable collection and
        // rebuilds its groups on every Reset. Staying flat always needs a rewire, because the first
        // group exposes a brand new List instance on every filtering pass.
        return next == GroupedItemsSourceMode.Flat;
    }
}