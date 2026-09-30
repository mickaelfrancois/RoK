namespace Rok.Application.Player.Output;

/// <summary>Active output devices and the Windows default one at a given time.</summary>
public sealed record AudioDeviceSnapshot(IReadOnlyList<AudioDeviceDto> Active, string? DefaultId)
{
    public static AudioDeviceSnapshot Empty { get; } = new([], null);

    public bool IsActive(string? deviceId) => !string.IsNullOrEmpty(deviceId) && Active.Any(device => device.Id == deviceId);

    public bool IsSameAs(AudioDeviceSnapshot other) =>
        DefaultId == other.DefaultId && Active.Select(device => device.Id).SequenceEqual(other.Active.Select(device => device.Id));
}