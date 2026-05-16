using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class DoubleExtensionsTests
{
    [Theory(DisplayName = "EqualsZero should return true only for values within epsilon of zero")]
    [InlineData(0.0, true)]
    [InlineData(1e-7, true)]
    [InlineData(-1e-7, true)]
    [InlineData(1e-5, false)]
    [InlineData(-1e-5, false)]
    [InlineData(1.0, false)]
    public void EqualsZero_ShouldReturnTrueOnlyWithinEpsilonOfZero(double value, bool expected)
    {
        // Act
        bool result = value.EqualsZero();

        // Assert
        Assert.Equal(expected, result);
    }
}