namespace Rok.Services;

/// <summary>How the fields written into the tags (name, artist, year, genre) behave in the album edit dialog.</summary>
public enum AlbumTaggedFieldsMode
{
    /// <summary>The fields are not shown: writing tags is off, files wait for a write, or the album has no artist.</summary>
    Hidden,

    /// <summary>Every field can be edited.</summary>
    Editable,

    /// <summary>The artist is shown but locked, because the artist of a compilation cannot change.</summary>
    ArtistLocked
}