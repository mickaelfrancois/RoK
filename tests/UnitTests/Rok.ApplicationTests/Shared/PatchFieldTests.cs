using Rok.Shared;

namespace Rok.ApplicationTests.Shared;

public class PatchFieldTests
{
    [Fact(DisplayName = "Default constructor should leave field unset with default value")]
    public void DefaultConstructor_ShouldLeaveFieldUnset()
    {
        // Act
        PatchField<string> sut = new();

        // Assert
        Assert.False(sut.IsSet);
        Assert.Null(sut.Value);
    }

    [Fact(DisplayName = "Value constructor should mark field as set with provided value")]
    public void ValueConstructor_ShouldMarkAsSet()
    {
        // Act
        PatchField<string> sut = new("hello");

        // Assert
        Assert.True(sut.IsSet);
        Assert.Equal("hello", sut.Value);
    }

    [Fact(DisplayName = "TryGetValue should return true when set and expose the value")]
    public void TryGetValue_WhenSet_ShouldReturnTrueAndValue()
    {
        // Arrange
        PatchField<int> sut = new(42);

        // Act
        bool ok = sut.TryGetValue(out int value);

        // Assert
        Assert.True(ok);
        Assert.Equal(42, value);
    }

    [Fact(DisplayName = "TryGetValue should return false when field is unset")]
    public void TryGetValue_WhenUnset_ShouldReturnFalse()
    {
        // Arrange
        PatchField<int> sut = new();

        // Act
        bool ok = sut.TryGetValue(out int value);

        // Assert
        Assert.False(ok);
        Assert.Equal(default, value);
    }

    [Fact(DisplayName = "Set should mark the field as set with the new value")]
    public void Set_ShouldMarkAsSetWithNewValue()
    {
        // Arrange
        PatchField<string> sut = new();

        // Act
        sut.Set("updated");

        // Assert
        Assert.True(sut.IsSet);
        Assert.Equal("updated", sut.Value);
    }

    [Fact(DisplayName = "Clear should reset the field to its default unset state")]
    public void Clear_ShouldResetToDefaultUnsetState()
    {
        // Arrange
        PatchField<string> sut = new("anything");

        // Act
        sut.Clear();

        // Assert
        Assert.False(sut.IsSet);
        Assert.Null(sut.Value);
    }

    [Fact(DisplayName = "ToString should return value when set")]
    public void ToString_WhenSet_ShouldReturnValue()
    {
        // Arrange
        PatchField<string> sut = new("set-value");

        // Act
        string? str = sut.ToString();

        // Assert
        Assert.Equal("set-value", str);
    }

    [Fact(DisplayName = "ToString should return NotSet placeholder when unset")]
    public void ToString_WhenUnset_ShouldReturnNotSetPlaceholder()
    {
        // Arrange
        PatchField<string> sut = new();

        // Act
        string? str = sut.ToString();

        // Assert
        Assert.Equal("NotSet", str);
    }
}
