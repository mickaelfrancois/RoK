using Microsoft.Extensions.Logging.Abstractions;
using Rok.Infrastructure.Power;
using Windows.System.Power;

namespace Rok.Infrastructure.UnitTests.Power;

public class WindowsPowerStateProviderTests
{
    [Theory(DisplayName = "only_a_discharging_battery_counts_as_on_battery")]
    [InlineData(BatteryStatus.Discharging, true)]
    [InlineData(BatteryStatus.Charging, false)]
    [InlineData(BatteryStatus.Idle, false)]
    [InlineData(BatteryStatus.NotPresent, false)]
    public void IsDischarging_MapsStatus(BatteryStatus status, bool expected)
    {
        // Act
        var result = WindowsPowerStateProvider.IsDischarging(status);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "reading_the_power_state_never_throws")]
    public void IsOnBattery_NeverThrows()
    {
        // Arrange
        var provider = new WindowsPowerStateProvider(NullLogger<WindowsPowerStateProvider>.Instance);

        // Act
        var error = Record.Exception(() => provider.IsOnBattery);

        // Assert
        Assert.Null(error);
    }
}