namespace Rok.Services.Diagnostics;

/// <summary>
/// Recognizes the null item the framework sometimes feeds to the generated x:Bind code of a list
/// item template. Only that container misses its bindings, so the app can carry on instead of
/// being torn down by the null-pointer error the framework rethrows.
/// </summary>
public static class RecoverableBindingFailure
{
    /// <summary>
    /// Oldest first-chance throw still considered the origin of the unhandled error.
    /// </summary>
    public const long MaxAgeMs = 1000;

    private const int NullPointerHResult = unchecked((int)0x80004003);

    private const string GeneratedBindingsFrame = "_Bindings.ProcessBindings";


    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="exception"/> is a null-pointer error whose
    /// recent first-chance throw happened inside generated x:Bind code.
    /// </summary>
    public static bool IsMatch(Exception exception, FirstChanceSite? site)
    {
        if (exception.HResult != NullPointerHResult || site is null)
            return false;

        return site.AgeMs <= MaxAgeMs && site.Frames.Contains(GeneratedBindingsFrame, StringComparison.Ordinal);
    }
}