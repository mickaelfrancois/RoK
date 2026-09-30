using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using Rok.Application.Player.Output;

namespace Rok.Infrastructure.Player.Output;

/// <summary>
/// Lists the active render devices and reports their changes. Windows raises the notifications on its audio worker
/// thread, where no audio API may be called: the handlers only arm a 250 ms timer, whose callback reads the devices on the thread pool
/// and raises <see cref="DevicesChanged"/> when the snapshot really changed.
/// </summary>
public sealed class WasapiDeviceService : IAudioDeviceService, IDisposable
{
    internal static readonly TimeSpan CoalescingDelay = TimeSpan.FromMilliseconds(250);

    private readonly ILogger<WasapiDeviceService> _logger;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDeviceNotificationClient _notifications;
    private readonly ITimer _refreshTimer;
    private readonly Lock _snapshotLock = new();
    private AudioDeviceSnapshot _lastSnapshot;
    private bool _disposed;

    public event EventHandler<AudioDeviceSnapshot>? DevicesChanged;

    public WasapiDeviceService(TimeProvider timeProvider, ILogger<WasapiDeviceService> logger)
    {
        _logger = logger;
        _enumerator = new MMDeviceEnumerator();
        _refreshTimer = timeProvider.CreateTimer(_ => Refresh(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _lastSnapshot = ReadSnapshot();
        _notifications = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
        _notifications.DeviceAdded += Notifications_DeviceChanged;
        _notifications.DeviceRemoved += Notifications_DeviceChanged;
        _notifications.DeviceStateChanged += Notifications_DeviceStateChanged;
        _notifications.DefaultDeviceChanged += Notifications_DefaultDeviceChanged;
    }

    public AudioDeviceSnapshot GetSnapshot()
    {
        AudioDeviceSnapshot snapshot = ReadSnapshot();

        lock (_snapshotLock)
        {
            _lastSnapshot = snapshot;
        }

        return snapshot;
    }

    private AudioDeviceSnapshot ReadSnapshot()
    {
        try
        {
            using MMDeviceEnumerator enumerator = new();
            List<AudioDeviceDto> active = [];

            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                {
                    active.Add(new AudioDeviceDto(device.ID, device.FriendlyName));
                }
            }

            string? defaultId = null;

            if (enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out MMDevice? defaultDevice) && defaultDevice is not null)
            {
                using (defaultDevice)
                {
                    defaultId = defaultDevice.ID;
                }
            }

            return new AudioDeviceSnapshot(active, defaultId);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Unable to list the audio output devices");
            return AudioDeviceSnapshot.Empty;
        }
    }

    private void Refresh()
    {
        if (_disposed)
            return;

        AudioDeviceSnapshot snapshot = ReadSnapshot();
        bool changed;

        lock (_snapshotLock)
        {
            changed = !snapshot.IsSameAs(_lastSnapshot);
            _lastSnapshot = snapshot;
        }

        if (!changed)
            return;

        _logger.LogInformation("Audio devices changed: {Count} active, default {DefaultId}", snapshot.Active.Count, snapshot.DefaultId);

        try
        {
            DevicesChanged?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply an audio device change");
        }
    }

    private void ScheduleRefresh()
    {
        if (!_disposed)
            _refreshTimer.Change(CoalescingDelay, Timeout.InfiniteTimeSpan);
    }

    private void Notifications_DeviceChanged(object? sender, DeviceNotificationEventArgs e) => ScheduleRefresh();

    private void Notifications_DeviceStateChanged(object? sender, DeviceStateChangedEventArgs e) => ScheduleRefresh();

    private void Notifications_DefaultDeviceChanged(object? sender, DefaultDeviceChangedEventArgs e)
    {
        if (e.Flow == DataFlow.Render && e.Role == Role.Multimedia)
            ScheduleRefresh();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _notifications.DeviceAdded -= Notifications_DeviceChanged;
        _notifications.DeviceRemoved -= Notifications_DeviceChanged;
        _notifications.DeviceStateChanged -= Notifications_DeviceStateChanged;
        _notifications.DefaultDeviceChanged -= Notifications_DefaultDeviceChanged;
        _notifications.Dispose();
        _refreshTimer.Dispose();
        _enumerator.Dispose();
    }
}