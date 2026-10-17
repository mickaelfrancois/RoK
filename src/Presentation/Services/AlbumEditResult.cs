using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>What an album edit changed.</summary>
/// <param name="Updated">True when the fields that are not written into tags were saved.</param>
/// <param name="Outcome">The outcome of the tag write, null when no tagged field was written.</param>
public sealed record AlbumEditResult(bool Updated, AlbumTagWriteOutcome? Outcome)
{
    /// <summary>Gets the result of an edit that changed nothing.</summary>
    public static AlbumEditResult Nothing { get; } = new(false, null);

    /// <summary>Gets a value indicating whether the database carries new tagged values after the edit.</summary>
    public bool TagsChanged => Outcome?.State is EArtistRenameState.Completed or EArtistRenameState.Partial;
}