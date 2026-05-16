using Rok.Shared.Extensions;

namespace Rok.ApplicationTests.Shared.Extensions;

public class FloatExtensionsTests
{
    [Theory(DisplayName = "EqualsZero should return true only for values within epsilon of zero")]
    [InlineData(0f, true)]
    [InlineData(1e-7f, true)]
    [InlineData(-1e-7f, true)]
    [InlineData(1e-5f, false)]
    [InlineData(-1e-5f, false)]
    [InlineData(1f, false)]
    public void EqualsZero_ShouldReturnTrueOnlyWithinEpsilonOfZero(float value, bool expected)
    {
        // Act
        bool result = value.EqualsZero();

        // Assert
        Assert.Equal(expected, result);
    }
}