namespace Rok.Services;

/// <summary>How the name field of the artist edit dialog behaves.</summary>
public enum ArtistNameMode
{
    /// <summary>The field is not shown: writing tags is turned off, so a rename is not allowed.</summary>
    Hidden,

    /// <summary>The field is shown but cannot be edited: an earlier rename is not finished.</summary>
    Locked,

    /// <summary>The field can be edited.</summary>
    Editable
}