namespace Rok.Application.Interfaces.Pictures;

/// <summary>
/// What happened to the image folder of an artist merged into another one.
/// </summary>
public enum ArtistFolderMergeOutcome
{
    /// <summary>No source folder, same folder for both names, no usable folder name, or the folder is shared with another living artist.</summary>
    NothingToDo,

    /// <summary>The target folder was absent or empty: the source folder was moved as a whole.</summary>
    Moved,

    /// <summary>The source files missing from the target were moved in. The target files were kept. The source folder was removed.</summary>
    Merged
}