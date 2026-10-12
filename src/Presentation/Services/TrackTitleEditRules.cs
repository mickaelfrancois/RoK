namespace Rok.Services;

/// <summary>Pure rules of the track title edit dialog.</summary>
public static class TrackTitleEditRules
{
    /// <summary>Removes the leading and trailing white space of a typed title.</summary>
    /// <param name="title">The typed title.</param>
    /// <returns>The trimmed title, empty when <paramref name="title"/> is null.</returns>
    public static string Normalize(string? title)
    {
        return title?.Trim() ?? string.Empty;
    }

    /// <summary>Tells whether the typed title can be saved.</summary>
    /// <param name="current">The title the track has now.</param>
    /// <param name="candidate">The typed title.</param>
    /// <returns><see langword="true"/> when the trimmed candidate is not empty and differs from <paramref name="current"/> (ordinal comparison).</returns>
    public static bool CanSave(string? current, string? candidate)
    {
        string normalized = Normalize(candidate);

        if (normalized.Length == 0)
            return false;

        return !string.Equals(normalized, Normalize(current), StringComparison.Ordinal);
    }

    /// <summary>Builds the live preview of the change.</summary>
    /// <param name="oldTitle">The title the track has now.</param>
    /// <param name="newTitle">The typed title.</param>
    /// <returns>The text "old → new".</returns>
    public static string FormatPreview(string? oldTitle, string? newTitle)
    {
        return $"{Normalize(oldTitle)} → {Normalize(newTitle)}";
    }
}