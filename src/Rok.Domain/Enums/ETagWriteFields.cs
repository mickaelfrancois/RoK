namespace Rok.Domain.Enums;

/// <summary>Tag fields of a track file that the database already holds but the file may not carry yet.</summary>
[Flags]
public enum ETagWriteFields
{
    /// <summary>No field.</summary>
    None = 0,

    /// <summary>Album name.</summary>
    Album = 1,

    /// <summary>Artist name.</summary>
    Artist = 2,

    /// <summary>Album year.</summary>
    Year = 4,

    /// <summary>First genre.</summary>
    Genre = 8,

    /// <summary>Disc number.</summary>
    Disc = 16
}