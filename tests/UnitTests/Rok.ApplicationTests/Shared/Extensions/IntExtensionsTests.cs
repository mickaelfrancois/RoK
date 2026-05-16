using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class IntExtensionsTests
{
    [Theory(DisplayName = "AreEquals should return true when both values are equal")]
    [InlineData(1, 1, true)]
    [InlineData(0, 0, true)]
    [InlineData(1, 2, false)]
    [InlineData(null, null, true)]
    [InlineData(null, 0, false)]
    [InlineData(0, null, false)]
    public void AreEquals_ShouldCompareNullableIntegers(int? a, int? b, bool expected)
    {
        // Act
        bool result = a.AreEquals(b);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "AreEquals should not treat null and int.MinValue as equal")]
    public void AreEquals_ShouldNotTreatNullAndMinValueAsEqual()
    {
        // Arrange
        int? a = null;
        int? b = int.MinValue;

        // Act
        bool result = a.AreEquals(b);

        // Assert
        Assert.False(result);
    }

    [Theory(DisplayName = "AreDifferent should be the inverse of AreEquals")]
    [InlineData(1, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(null, null, false)]
    [InlineData(null, 0, true)]
    public void AreDifferent_ShouldBeInverseOfAreEquals(int? a, int? b, bool expected)
    {
        // Act
        bool result = a.AreDifferent(b);

        // Assert
        Assert.Equal(expected, result);
    }
}