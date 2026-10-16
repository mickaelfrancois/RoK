using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>What the user typed in the name field compared with the current name.</summary>
public enum ArtistNameChangeKind
{
    /// <summary>The name is the same, nothing to rename.</summary>
    Unchanged,

    /// <summary>The name is empty or too long.</summary>
    Invalid,

    /// <summary>The name is a new valid name.</summary>
    Changed
}

/// <summary>Verdict on a typed artist name.</summary>
/// <param name="Kind">What the typed name means.</param>
/// <param name="Name">The normalized name, as the import would store it.</param>
public readonly record struct ArtistNameChange(ArtistNameChangeKind Kind, string Name);

/// <summary>Pure rules of the artist edit and rename screens.</summary>
public static class ArtistRenameRules
{
    /// <summary>Tells how the name field behaves.</summary>
    /// <param name="writeTagsEnabled">Whether writing tags is turned on in the options.</param>
    /// <param name="renamePendingFrom">The marker of an unfinished rename, null or empty when there is none.</param>
    /// <returns>The mode of the field.</returns>
    public static ArtistNameMode NameMode(bool writeTagsEnabled, string? renamePendingFrom)
    {
        if (!writeTagsEnabled)
            return ArtistNameMode.Hidden;

        return string.IsNullOrEmpty(renamePendingFrom) ? ArtistNameMode.Editable : ArtistNameMode.Locked;
    }

    /// <summary>Judges the typed name.</summary>
    /// <param name="currentName">The name the artist carries now.</param>
    /// <param name="typedName">What the user typed.</param>
    /// <returns>The verdict, with the normalized name.</returns>
    public static ArtistNameChange Evaluate(string currentName, string? typedName)
    {
        string name = ArtistNameRules.Normalize(typedName);

        if (name.Length == 0 || name.Length > ArtistRenameLimits.MaxNameLength)
            return new ArtistNameChange(ArtistNameChangeKind.Invalid, name);

        if (string.Equals(name, currentName, StringComparison.Ordinal))
            return new ArtistNameChange(ArtistNameChangeKind.Unchanged, name);

        return new ArtistNameChange(ArtistNameChangeKind.Changed, name);
    }

    /// <summary>Tells whether the rename can start from this preview.</summary>
    /// <param name="preview">What the rename would do.</param>
    /// <returns>True when nothing blocks the rename and no other artist carries the name.</returns>
    public static bool CanStart(ArtistRenamePreview preview)
    {
        return preview.Blockers.Count == 0 && preview.Collision is null;
    }
}