using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class CrossfadeDurationTests
{
    [Theory(DisplayName = "crossfade_duration_is_kept_within_bounds")]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(12, 12)]
    [InlineData(30, 12)]
    public void Clamp_KeepsWithinBounds(int stored, int expected)
    {
        // Act
        int result = CrossfadeDuration.Clamp(stored);

        // Assert
        Assert.Equal(expected, result);
    }
}