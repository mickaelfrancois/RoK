using Rok.ViewModels.Tracks.Services;

namespace Rok.PresentationTests.ViewModels.Tracks.Services;

public class TracksSelectionManagerTests
{
    [Fact(DisplayName = "A new selection manager should expose an empty selection")]
    public void NewSelectionManager_ShouldExposeEmptySelection()
    {
        // Arrange
        TracksSelectionManager sut = new();

        // Assert
        Assert.Equal(0, sut.SelectedCount);
        Assert.False(sut.IsSelectedItems);
    }

    [Fact(DisplayName = "Adding items should update SelectedCount and IsSelectedItems")]
    public void AddingToSelected_ShouldUpdateCounts()
    {
        // Arrange
        TracksSelectionManager sut = new();

        // Act
        sut.Selected.Add(new object());

        // Assert
        Assert.Equal(1, sut.SelectedCount);
        Assert.True(sut.IsSelectedItems);
    }

    [Fact(DisplayName = "Selected collection changes should fire SelectionChanged")]
    public void SelectedChanges_ShouldFireSelectionChanged()
    {
        // Arrange
        TracksSelectionManager sut = new();
        int eventCount = 0;
        sut.SelectionChanged += (_, _) => eventCount++;

        // Act
        sut.Selected.Add(new object());

        // Assert
        Assert.Equal(1, eventCount);
    }
}
