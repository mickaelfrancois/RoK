using Rok.ViewModels.Albums.Services;

namespace Rok.PresentationTests.ViewModels.Albums.Services;

public class AlbumsSelectionManagerTests
{
    [Fact(DisplayName = "A new selection manager should expose an empty selection")]
    public void NewSelectionManager_ShouldExposeEmptySelection()
    {
        // Arrange
        AlbumsSelectionManager sut = new();

        // Assert
        Assert.Equal(0, sut.SelectedCount);
        Assert.False(sut.IsSelectedItems);
        Assert.Empty(sut.SelectedItems);
    }

    [Fact(DisplayName = "Adding items to Selected should update SelectedCount and IsSelectedItems")]
    public void AddingToSelected_ShouldUpdateCounts()
    {
        // Arrange
        AlbumsSelectionManager sut = new();

        // Act
        sut.Selected.Add(new object());
        sut.Selected.Add(new object());

        // Assert
        Assert.Equal(2, sut.SelectedCount);
        Assert.True(sut.IsSelectedItems);
    }

    [Fact(DisplayName = "Selected collection changes should fire SelectionChanged once per change")]
    public void SelectedChanges_ShouldFireSelectionChanged()
    {
        // Arrange
        AlbumsSelectionManager sut = new();
        int eventCount = 0;
        sut.SelectionChanged += (_, _) => eventCount++;

        // Act
        sut.Selected.Add(new object());
        sut.Selected.Add(new object());
        sut.Selected.RemoveAt(0);

        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact(DisplayName = "Selected collection changes should fire OnPropertyChanged for the derived properties")]
    public void SelectedChanges_ShouldFirePropertyChangedForDerivedProperties()
    {
        // Arrange
        AlbumsSelectionManager sut = new();
        List<string?> propertyNames = new();
        sut.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName);

        // Act
        sut.Selected.Add(new object());

        // Assert
        Assert.Contains(nameof(sut.SelectedItems), propertyNames);
        Assert.Contains(nameof(sut.SelectedCount), propertyNames);
        Assert.Contains(nameof(sut.IsSelectedItems), propertyNames);
    }
}