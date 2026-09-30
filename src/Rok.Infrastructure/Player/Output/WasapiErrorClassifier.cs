using System.Runtime.InteropServices;
using Rok.Application.Player.Output;

namespace Rok.Infrastructure.Player.Output;

/// <summary>Maps the WASAPI error codes raised while opening an exclusive output to a fallback reason.</summary>
internal static class WasapiErrorClassifier
{
    internal const int DeviceInvalidated = unchecked((int)0x88890004);
    internal const int UnsupportedFormat = unchecked((int)0x88890008);
    internal const int DeviceInUse = unchecked((int)0x8889000A);
    internal const int ExclusiveModeNotAllowed = unchecked((int)0x8889000E);

    public static EExclusiveFallbackReason Classify(int hresult) => hresult switch
    {
        UnsupportedFormat => EExclusiveFallbackReason.FormatRefused,
        DeviceInUse => EExclusiveFallbackReason.DeviceBusy,
        ExclusiveModeNotAllowed => EExclusiveFallbackReason.ExclusiveDisabled,
        _ => EExclusiveFallbackReason.Other
    };

    /// <summary>The device disappeared: a lost output, not a reason to fall back to the shared mode.</summary>
    public static bool IsDeviceInvalidated(int hresult) => hresult == DeviceInvalidated;

    /// <summary>
    /// Reason to fall back to the shared mode after an exclusive open failed, or <c>null</c> when the exception must
    /// propagate: a vanished device is a lost output, and an unexpected exception is not a device refusal.
    /// <see cref="NotSupportedException"/> is what <c>WasapiPlayer</c> raises when the device refuses the sample rate.
    /// </summary>
    public static EExclusiveFallbackReason? ClassifyExclusiveFailure(Exception exception) => exception switch
    {
        COMException com when IsDeviceInvalidated(com.HResult) => null,
        COMException com => Classify(com.HResult),
        NotSupportedException => EExclusiveFallbackReason.FormatRefused,
        _ => null
    };
}