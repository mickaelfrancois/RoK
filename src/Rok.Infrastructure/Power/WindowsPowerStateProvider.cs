using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Windows.System.Power;

namespace Rok.Infrastructure.Power;

/// <summary>Reads the battery state from <see cref="PowerManager"/> on each call.</summary>
public sealed class WindowsPowerStateProvider : IPowerStateProvider
{
    private readonly ILogger<WindowsPowerStateProvider> _logger;
    private int _failureLogged;

    /// <summary>Initializes a new instance of the <see cref="WindowsPowerStateProvider"/> class.</summary>
    /// <param name="logger">Logger.</param>
    public WindowsPowerStateProvider(ILogger<WindowsPowerStateProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsOnBattery
    {
        get
        {
            try
            {
                return IsDischarging(PowerManager.BatteryStatus);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _failureLogged, 1) == 0)
                    _logger.LogWarning(ex, "Power: could not read the battery status, assuming mains power");

                return false;
            }
        }
    }

    /// <summary>Maps a battery status to "on battery": only a discharging battery counts.</summary>
    /// <param name="status">Status reported by Windows.</param>
    public static bool IsDischarging(BatteryStatus status) => status == BatteryStatus.Discharging;
}