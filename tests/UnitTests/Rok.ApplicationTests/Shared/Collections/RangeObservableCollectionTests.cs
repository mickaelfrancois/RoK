using System.Collections.Specialized;
using System.ComponentModel;
using Rok.Shared.Collections;

namespace Rok.ApplicationTests.Shared.Collections;

public class RangeObservableCollectionTests
{
    [Fact(DisplayName = "AddRange should append items and raise a single Reset notification")]
    public void AddRange_ShouldAppendItemsAndRaiseSingleResetNotification()
    {
        // Arrange
        RangeObservableCollection<int> sut = new() { 1, 2 };
        List<NotifyCollectionChangedAction> events = new();
        sut.CollectionChanged += (_, e) => events.Add(e.Action);

        // Act
        sut.AddRange(new[] { 3, 4, 5 });

        // Assert
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, sut.ToArray());
        Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, events[0]);
    }

    [Fact(DisplayName = "InitWithAddRange should clear existing items before adding the new range")]
    public void InitWithAddRange_ShouldClearBeforeAdding()
    {
        // Arrange
        RangeObservableCollection<string> sut = new() { "old1", "old2" };

        // Act
        sut.InitWithAddRange(new[] { "new1", "new2", "new3" });

        // Assert
        Assert.Equal(new[] { "new1", "new2", "new3" }, sut.ToArray());
    }

    [Fact(DisplayName = "UpdateItem should fire a Replace notification when the item is in the collection")]
    public void UpdateItem_ShouldFireReplaceNotification()
    {
        // Arrange
        RangeObservableCollection<string> sut = new() { "a", "b", "c" };
        NotifyCollectionChangedEventArgs? captured = null;
        sut.CollectionChanged += (_, e) => captured = e;

        // Act
        sut.UpdateItem("b");

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(NotifyCollectionChangedAction.Replace, captured!.Action);
        Assert.Equal(1, captured.NewStartingIndex);
    }

    [Fact(DisplayName = "UpdateItem should not raise any notification when the item is not in the collection")]
    public void UpdateItem_WithUnknownItem_ShouldNotRaiseNotification()
    {
        // Arrange
        RangeObservableCollection<string> sut = new() { "a", "b" };
        bool raised = false;
        sut.CollectionChanged += (_, _) => raised = true;

        // Act
        sut.UpdateItem("z");

        // Assert
        Assert.False(raised);
    }

    [Fact(DisplayName = "when_init_with_add_range_then_a_single_reset_is_raised")]
    public void InitWithAddRange_ShouldRaiseASingleResetNotification()
    {
        // Arrange
        RangeObservableCollection<int> sut = new() { 1, 2 };
        List<NotifyCollectionChangedAction> events = new();
        sut.CollectionChanged += (_, e) => events.Add(e.Action);

        // Act
        sut.InitWithAddRange(new[] { 3, 4, 5 });

        // Assert
        Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, events[0]);
    }

    [Fact(DisplayName = "when_init_with_add_range_then_subscriber_observes_final_count")]
    public void InitWithAddRange_ShouldNeverExposeTheIntermediateEmptyState()
    {
        // Arrange
        RangeObservableCollection<int> sut = new() { 1, 2 };
        List<int> observedCounts = new();
        sut.CollectionChanged += (_, _) => observedCounts.Add(sut.Count);

        // Act
        sut.InitWithAddRange(new[] { 3, 4, 5 });

        // Assert
        Assert.Equal(new[] { 3 }, observedCounts);
    }

    [Fact(DisplayName = "when_init_with_add_range_then_count_property_changed_is_raised_once")]
    public void InitWithAddRange_ShouldRaiseCountPropertyChangedOnce()
    {
        // Arrange
        RangeObservableCollection<int> sut = new() { 1, 2 };
        List<string?> changedProperties = new();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        // Act
        sut.InitWithAddRange(new[] { 3, 4, 5 });

        // Assert
        Assert.Single(changedProperties, name => name == nameof(sut.Count));
    }

    [Fact(DisplayName = "when_init_with_add_range_with_an_empty_range_then_collection_is_cleared")]
    public void InitWithAddRange_WithAnEmptyRange_ShouldClearTheCollection()
    {
        // Arrange
        RangeObservableCollection<int> sut = new() { 1, 2 };
        List<NotifyCollectionChangedAction> events = new();
        sut.CollectionChanged += (_, e) => events.Add(e.Action);

        // Act
        sut.InitWithAddRange(Array.Empty<int>());

        // Assert
        Assert.Empty(sut);
        Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, events[0]);
    }

    [Fact(DisplayName = "when_init_with_add_range_on_an_empty_collection_then_items_are_added")]
    public void InitWithAddRange_OnAnEmptyCollection_ShouldAddTheItems()
    {
        // Arrange
        RangeObservableCollection<string> sut = new();

        // Act
        sut.InitWithAddRange(new[] { "a", "b" });

        // Assert
        Assert.Equal(new[] { "a", "b" }, sut.ToArray());
    }
}