using System.Runtime.InteropServices;
using NAudio;
using NAudio.Wave;
using Rok.Application.Player.Output;

namespace Rok.Infrastructure.Player.Output;

/// <summary>An initialised output on a device, with the mode really obtained.</summary>
/// <param name="player">Output already initialised with its source.</param>
/// <param name="deviceId">Identifier of the device the output plays on.</param>
/// <param name="isOnPreferred">The device is the requested one (always true when following the Windows default).</param>
/// <param name="actualMode">Sharing mode really in use.</param>
/// <param name="fallbackReason">Why exclusive was requested but the output runs shared; <c>null</c> otherwise.</param>
/// <param name="exclusiveFormat">Format negotiated in exclusive mode; <c>null</c> for a shared output.</param>
/// <param name="ownedResource">Device object released with the output.</param>
public sealed class AudioOutputHandle(IWavePlayer player, string deviceId, bool isOnPreferred, EAudioOutputMode actualMode, EExclusiveFallbackReason? fallbackReason, WaveFormat? exclusiveFormat, IDisposable? ownedResource = null) : IDisposable
{
    private bool _disposed;

    public IWavePlayer Player { get; } = player;

    public string DeviceId { get; } = deviceId;

    public bool IsOnPreferred { get; } = isOnPreferred;

    public EAudioOutputMode ActualMode { get; } = actualMode;

    public EExclusiveFallbackReason? FallbackReason { get; } = fallbackReason;

    public WaveFormat? ExclusiveFormat { get; } = exclusiveFormat;

    /// <summary>Stops and releases the output. Never call it from the output's own playback thread.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            Player.Stop();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or MmException)
        {
            // The device may already be gone; releasing it is all that matters here.
        }

        Player.Dispose();
        ownedResource?.Dispose();
    }
}