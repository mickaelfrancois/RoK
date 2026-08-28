using Rok.Commons;

namespace Rok.PresentationTests.Commons;

public class GroupedItemsSourcePlannerTests
{
    [Fact(DisplayName = "when_grouping_is_enabled_and_groups_exist_then_mode_is_grouped")]
    public void ResolveMode_WithGroupingEnabledAndGroups_ShouldReturnGrouped()
    {
        // Arrange
        // Act
        GroupedItemsSourceMode mode = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled: true, hasGroups: true, current: GroupedItemsSourceMode.None);

        // Assert
        Assert.Equal(GroupedItemsSourceMode.Grouped, mode);
    }

    [Fact(DisplayName = "when_grouping_is_disabled_and_groups_exist_then_mode_is_flat")]
    public void ResolveMode_WithGroupingDisabledAndGroups_ShouldReturnFlat()
    {
        // Arrange
        // Act
        GroupedItemsSourceMode mode = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled: false, hasGroups: true, current: GroupedItemsSourceMode.None);

        // Assert
        Assert.Equal(GroupedItemsSourceMode.Flat, mode);
    }

    [Fact(DisplayName = "when_there_is_no_group_and_current_mode_is_grouped_then_mode_stays_grouped")]
    public void ResolveMode_WithoutGroupWhileGrouped_ShouldStayGrouped()
    {
        // Arrange
        // Act
        GroupedItemsSourceMode mode = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled: true, hasGroups: false, current: GroupedItemsSourceMode.Grouped);

        // Assert
        Assert.Equal(GroupedItemsSourceMode.Grouped, mode);
    }

    [Fact(DisplayName = "when_there_is_no_group_and_current_mode_is_flat_then_mode_stays_flat")]
    public void ResolveMode_WithoutGroupWhileFlat_ShouldStayFlat()
    {
        // Arrange
        // Act
        GroupedItemsSourceMode mode = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled: true, hasGroups: false, current: GroupedItemsSourceMode.Flat);

        // Assert
        Assert.Equal(GroupedItemsSourceMode.Flat, mode);
    }

    [Fact(DisplayName = "when_there_is_no_group_and_nothing_is_wired_then_mode_is_flat")]
    public void ResolveMode_WithoutGroupAndNothingWired_ShouldReturnFlat()
    {
        // Arrange
        // Act
        GroupedItemsSourceMode mode = GroupedItemsSourcePlanner.ResolveMode(isGroupingEnabled: true, hasGroups: false, current: GroupedItemsSourceMode.None);

        // Assert
        Assert.Equal(GroupedItemsSourceMode.Flat, mode);
    }

    [Fact(DisplayName = "when_mode_stays_grouped_then_rewire_is_not_required")]
    public void RequiresRewire_StayingGrouped_ShouldReturnFalse()
    {
        // Arrange
        // Act
        bool requiresRewire = GroupedItemsSourcePlanner.RequiresRewire(GroupedItemsSourceMode.Grouped, GroupedItemsSourceMode.Grouped);

        // Assert
        Assert.False(requiresRewire);
    }

    [Fact(DisplayName = "when_mode_stays_flat_then_rewire_is_required")]
    public void RequiresRewire_StayingFlat_ShouldReturnTrue()
    {
        // Arrange
        // Act
        bool requiresRewire = GroupedItemsSourcePlanner.RequiresRewire(GroupedItemsSourceMode.Flat, GroupedItemsSourceMode.Flat);

        // Assert
        Assert.True(requiresRewire);
    }

    [Theory(DisplayName = "when_mode_changes_then_rewire_is_required")]
    [InlineData(GroupedItemsSourceMode.None, GroupedItemsSourceMode.Grouped)]
    [InlineData(GroupedItemsSourceMode.None, GroupedItemsSourceMode.Flat)]
    [InlineData(GroupedItemsSourceMode.Grouped, GroupedItemsSourceMode.None)]
    [InlineData(GroupedItemsSourceMode.Grouped, GroupedItemsSourceMode.Flat)]
    [InlineData(GroupedItemsSourceMode.Flat, GroupedItemsSourceMode.None)]
    [InlineData(GroupedItemsSourceMode.Flat, GroupedItemsSourceMode.Grouped)]
    public void RequiresRewire_WhenModeChanges_ShouldReturnTrue(GroupedItemsSourceMode current, GroupedItemsSourceMode next)
    {
        // Arrange
        // Act
        bool requiresRewire = GroupedItemsSourcePlanner.RequiresRewire(current, next);

        // Assert
        Assert.True(requiresRewire);
    }

    [Fact(DisplayName = "when_nothing_is_wired_and_mode_stays_none_then_rewire_is_not_required")]
    public void RequiresRewire_StayingNone_ShouldReturnFalse()
    {
        // Arrange
        // Act
        bool requiresRewire = GroupedItemsSourcePlanner.RequiresRewire(GroupedItemsSourceMode.None, GroupedItemsSourceMode.None);

        // Assert
        Assert.False(requiresRewire);
    }
}