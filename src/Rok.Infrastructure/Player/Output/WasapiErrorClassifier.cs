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
}