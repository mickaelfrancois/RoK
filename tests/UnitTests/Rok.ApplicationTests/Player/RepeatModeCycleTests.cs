using Rok.Application.Player;

namespace Rok.ApplicationTests.Player;

public class RepeatModeCycleTests
{
    [Theory(DisplayName = "repeat_mode_cycles_off_all_one_then_off")]
    [InlineData(ERepeatMode.Off, ERepeatMode.All)]
    [InlineData(ERepeatMode.All, ERepeatMode.One)]
    [InlineData(ERepeatMode.One, ERepeatMode.Off)]
    public void Next_returns_the_following_mode(ERepeatMode current, ERepeatMode expected)
    {
        // Arrange
        // Act
        ERepeatMode result = RepeatModeCycle.Next(current);

        // Assert
        Assert.Equal(expected, result);
    }
}