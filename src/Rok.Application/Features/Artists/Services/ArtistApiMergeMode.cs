namespace Rok.Application.Features.Artists.Services;

/// <summary>
/// How the data of the music API is merged into the artist.
/// </summary>
public enum ArtistApiMergeMode
{
    /// <summary>Empty fields are filled, and links that differ are replaced.</summary>
    Default,

    /// <summary>Only empty fields are filled, links included. Nothing already present is replaced.</summary>
    FillEmptyOnly
}