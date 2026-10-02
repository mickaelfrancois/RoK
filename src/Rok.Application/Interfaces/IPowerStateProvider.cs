namespace Rok.Application.Interfaces;

/// <summary>Tells whether the machine currently runs on battery, to skip costly background work.</summary>
public interface IPowerStateProvider
{
    /// <summary>Gets a value indicating whether the battery is discharging. A desktop without battery is never on battery.</summary>
    bool IsOnBattery { get; }
}