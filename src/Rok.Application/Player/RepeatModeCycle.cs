namespace Rok.Application.Player;

/// <summary>Cycles through the repeat modes: Off, All, One, then Off again.</summary>
public static class RepeatModeCycle
{
    /// <summary>Returns the mode that follows <paramref name="current"/> in the cycle.</summary>
    public static ERepeatMode Next(ERepeatMode current) => current switch
    {
        ERepeatMode.Off => ERepeatMode.All,
        ERepeatMode.All => ERepeatMode.One,
        _ => ERepeatMode.Off
    };
}