using System.Collections.Specialized;
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
}
