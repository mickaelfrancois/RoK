using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class ListExtensionsTests
{
    [Fact(DisplayName = "Shuffle should keep all original elements")]
    public void Shuffle_ShouldKeepAllOriginalElements()
    {
        // Arrange
        List<int> list = new() { 1, 2, 3, 4, 5 };
        List<int> original = new(list);

        // Act
        list.Shuffle();

        // Assert
        Assert.Equal(original.Count, list.Count);
        Assert.All(original, item => Assert.Contains(item, list));
    }

    [Fact(DisplayName = "Shuffle on a single-element list should leave it unchanged")]
    public void Shuffle_SingleElement_ShouldLeaveListUnchanged()
    {
        // Arrange
        List<int> list = new() { 42 };

        // Act
        list.Shuffle();

        // Assert
        Assert.Single(list);
        Assert.Equal(42, list[0]);
    }

    [Fact(DisplayName = "Shuffle on an empty list should not throw")]
    public void Shuffle_EmptyList_ShouldNotThrow()
    {
        // Arrange
        List<int> list = new();

        // Act & Assert
        list.Shuffle();
        Assert.Empty(list);
    }
}
