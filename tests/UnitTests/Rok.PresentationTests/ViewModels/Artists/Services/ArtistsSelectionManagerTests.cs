using Rok.ViewModels.Artists.Services;

namespace Rok.PresentationTests.ViewModels.Artists.Services;

public class ArtistsSelectionManagerTests
{
    [Fact(DisplayName = "A new selection manager should expose an empty selection")]
    public void NewSelectionManager_ShouldExposeEmptySelection()
    {
        // Arrange
        ArtistsSelectionManager sut = new();

        // Assert
        Assert.Equal(0, sut.SelectedCount);
        Assert.False(sut.IsSelectedItems);
    }

    [Fact(DisplayName = "Adding items should update SelectedCount and IsSelectedItems")]
    public void AddingToSelected_ShouldUpdateCounts()
    {
        // Arrange
        ArtistsSelectionManager sut = new();

        // Act
        sut.Selected.Add(new object());
        sut.Selected.Add(new object());

        // Assert
        Assert.Equal(2, sut.SelectedCount);
        Assert.True(sut.IsSelectedItems);
    }

    [Fact(DisplayName = "Selected collection changes should fire SelectionChanged")]
    public void SelectedChanges_ShouldFireSelectionChanged()
    {
        // Arrange
        ArtistsSelectionManager sut = new();
        int eventCount = 0;
        sut.SelectionChanged += (_, _) => eventCount++;

        // Act
        sut.Selected.Add(new object());
        sut.Selected.RemoveAt(0);

        // Assert
        Assert.Equal(2, eventCount);
    }
}
