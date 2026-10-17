using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>What the artist edit changed.</summary>
/// <param name="Updated">True when the fields of the dialog were saved.</param>
/// <param name="Rename">The outcome of the rename when one was started, otherwise null.</param>
public sealed record ArtistEditResult(bool Updated, ArtistRenameOutcome? Rename)
{
    /// <summary>Gets the result of an edit that changed nothing.</summary>
    public static ArtistEditResult Nothing { get; } = new(false, null);

    /// <summary>Gets a value indicating whether the database carries a new name after the edit.</summary>
    public bool NameChanged => Rename?.State is EArtistRenameState.Completed or EArtistRenameState.Partial;

    /// <summary>Gets the outcome of the merge when one was started, otherwise null.</summary>
    public ArtistMergeOutcome? Merge { get; init; }

    /// <summary>Gets a value indicating whether the database changed because of a merge.</summary>
    public bool MergeApplied => Merge?.State is EArtistRenameState.Completed or EArtistRenameState.Partial;
}