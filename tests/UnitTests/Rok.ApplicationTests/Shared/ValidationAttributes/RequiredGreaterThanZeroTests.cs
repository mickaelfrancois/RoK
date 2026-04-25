using Rok.Shared.ValidationAttributes;

namespace Rok.ApplicationTests.Shared.ValidationAttributes;

public class RequiredGreaterThanZeroTests
{
    [Theory(DisplayName = "IsValid should return true only when value parses to a positive integer")]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("not-a-number", false)]
    [InlineData("1", true)]
    [InlineData("42", true)]
    public void IsValid_StringValues_ShouldReturnExpected(string? value, bool expected)
    {
        // Arrange
        RequiredGreaterThanZero sut = new();

        // Act
        bool result = sut.IsValid(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "IsValid should accept integer values that are strictly positive")]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    public void IsValid_IntegerValues_ShouldReturnExpected(int value, bool expected)
    {
        // Arrange
        RequiredGreaterThanZero sut = new();

        // Act
        bool result = sut.IsValid(value);

        // Assert
        Assert.Equal(expected, result);
    }
}
