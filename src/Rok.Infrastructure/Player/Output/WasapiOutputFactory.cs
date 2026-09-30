using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Rok.Application.Player.Output;

namespace Rok.Infrastructure.Player.Output;

/// <summary>Opens WASAPI outputs, shared or exclusive, on the device resolved by <see cref="AudioDevicePolicy"/>.</summary>
/// <remarks>
/// Each <see cref="WasapiPlayer"/> is built without a <see cref="SynchronizationContext"/>, so that
/// <see cref="WasapiPlayer.PlaybackStopped"/> is raised on the player's own render thread whatever the calling thread.
/// </remarks>
public sealed class WasapiOutputFactory(IAudioDeviceService deviceService, ILogger<WasapiOutputFactory> logger) : IAudioOutputFactory
{
    internal const int LatencyMilliseconds = 200;

    public AudioOutputHandle Open(AudioOutputTarget target, ISampleProvider source, int nativeBitsPerSample)
    {
        string deviceId = AudioDevicePolicy.ResolveDeviceId(target.PreferredDeviceId, deviceService.GetSnapshot())
            ?? throw new InvalidOperationException("No audio output device is available.");

        bool isOnPreferred = target.FollowsWindowsDefault || deviceId == target.PreferredDeviceId;
        EExclusiveFallbackReason? fallbackReason = null;

        if (target.Mode == EAudioOutputMode.Exclusive)
        {
            AudioOutputHandle? exclusive = TryOpenExclusive(deviceId, isOnPreferred, source, nativeBitsPerSample, out EExclusiveFallbackReason reason);

            if (exclusive is not null)
                return exclusive;

            fallbackReason = reason;
            logger.LogWarning("Exclusive output refused ({Reason}), playing through the shared mixer", reason);
        }

        return OpenShared(deviceId, isOnPreferred, source, fallbackReason);
    }

    private static AudioOutputHandle OpenShared(string deviceId, bool isOnPreferred, ISampleProvider source, EExclusiveFallbackReason? fallbackReason)
    {
        MMDevice device = GetDevice(deviceId);
        WasapiPlayer? output = null;

        try
        {
            output = SynchronizationContextScope.RunDetached(() => new WasapiPlayerBuilder()
                .WithDevice(device)
                .WithSharedMode()
                .WithEventSync()
                .WithLatency(LatencyMilliseconds)
                .Build());
            output.Init(new SampleToWaveProvider(source));

            return new AudioOutputHandle(output, deviceId, isOnPreferred, EAudioOutputMode.Shared, fallbackReason, null, device);
        }
        catch
        {
            output?.Dispose();
            device.Dispose();
            throw;
        }
    }

    private AudioOutputHandle? TryOpenExclusive(string deviceId, bool isOnPreferred, ISampleProvider source, int nativeBitsPerSample, out EExclusiveFallbackReason reason)
    {
        reason = EExclusiveFallbackReason.FormatRefused;

        foreach (WaveFormatExtensible candidate in ExclusiveFormatLadder.Candidates(source.WaveFormat.SampleRate, source.WaveFormat.Channels, nativeBitsPerSample))
        {
            try
            {
                AudioOutputHandle? handle = TryOpenExclusiveFormat(deviceId, isOnPreferred, source, candidate);

                if (handle is not null)
                    return handle;
            }
            catch (Exception ex) when (WasapiErrorClassifier.ClassifyExclusiveFailure(ex) is EExclusiveFallbackReason classified)
            {
                reason = classified;

                logger.LogInformation(ex, "Exclusive output refused for {Format}: {ExceptionType} (0x{HResult:X8})", candidate, ex.GetType().Name, ex.HResult);

                if (reason != EExclusiveFallbackReason.FormatRefused)
                    return null;
            }
        }

        return null;
    }

    private AudioOutputHandle? TryOpenExclusiveFormat(string deviceId, bool isOnPreferred, ISampleProvider source, WaveFormatExtensible format)
    {
        if (!IsSupported(deviceId, format))
            return null;

        MMDevice device = GetDevice(deviceId);
        WasapiPlayer? output = null;

        try
        {
            output = SynchronizationContextScope.RunDetached(() => new WasapiPlayerBuilder()
                .WithDevice(device)
                .WithExclusiveMode()
                .WithPollingSync()
                .WithLatency(LatencyMilliseconds)
                .Build());
            output.Init(new FloatToPcmWaveProvider(source, format));

            if (!ExclusiveFormatLadder.AreEquivalent(output.OutputWaveFormat, format))
            {
                logger.LogWarning("Exclusive output negotiated {Actual} instead of {Expected}", output.OutputWaveFormat, format);
                output.Dispose();
                device.Dispose();
                return null;
            }

            logger.LogInformation("Exclusive output opened in {Format}", format);

            return new AudioOutputHandle(output, deviceId, isOnPreferred, EAudioOutputMode.Exclusive, null, format, device);
        }
        catch
        {
            output?.Dispose();
            device.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Checked before <see cref="WasapiPlayer.Init"/>, which would otherwise adapt the bit depth itself (not bit-exact)
    /// when the format is refused. Uses its own device instance so the client released here is never the one of the output.
    /// </summary>
    private static bool IsSupported(string deviceId, WaveFormat format)
    {
        using MMDevice device = GetDevice(deviceId);
        using AudioClient client = device.CreateAudioClient();

        return client.IsFormatSupported(AudioClientShareMode.Exclusive, format);
    }

    private static MMDevice GetDevice(string deviceId)
    {
        using MMDeviceEnumerator enumerator = new();

        return enumerator.GetDevice(deviceId);
    }
}