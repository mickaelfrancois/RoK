namespace Rok.Application.Player.Output;

/// <summary>Why an exclusive output fell back to the shared mode.</summary>
public enum EExclusiveFallbackReason
{
    /// <summary>The device accepts none of the formats offered for the file.</summary>
    FormatRefused = 0,

    /// <summary>Another application holds the device in exclusive mode.</summary>
    DeviceBusy = 1,

    /// <summary>Windows forbids exclusive control of the device.</summary>
    ExclusiveDisabled = 2,

    /// <summary>Any other failure while opening the exclusive output.</summary>
    Other = 3
}