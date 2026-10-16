namespace Rok.Application.Interfaces.Pictures;

/// <summary>
/// What happens, or happened, to an artist image folder when the artist is renamed.
/// </summary>
public enum ArtistFolderMoveOutcome
{
    /// <summary>No folder to move, or the folder name does not change.</summary>
    NothingToDo,

    /// <summary>The folder is moved to a free name, or renamed with a different case only.</summary>
    Move,

    /// <summary>The target folder exists but is empty: it is removed, then the folder is moved.</summary>
    ReplaceEmptyTarget,

    /// <summary>The target folder holds orphan images: the images of the renamed artist win.</summary>
    Merge,

    /// <summary>The target folder belongs to another living artist, or the new name gives no usable folder name.</summary>
    Blocked
}