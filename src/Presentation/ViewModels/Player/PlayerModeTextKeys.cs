using Rok.Application.Player;

namespace Rok.ViewModels.Player;

/// <summary>Resource keys of the texts describing the repeat and shuffle modes.</summary>
public static class PlayerModeTextKeys
{
    public static string Repeat(ERepeatMode mode) => mode switch
    {
        ERepeatMode.All => "playerRepeatAll",
        ERepeatMode.One => "playerRepeatOne",
        _ => "playerRepeatOff"
    };

    public static string Shuffle(bool isEnabled) => isEnabled ? "playerShuffleOn" : "playerShuffleOff";
}