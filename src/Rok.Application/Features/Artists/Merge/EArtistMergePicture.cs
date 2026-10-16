namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// Which artist picture survives a merge.
/// </summary>
public enum EArtistMergePicture
{
    /// <summary>The kept artist has a picture; it wins.</summary>
    Target,

    /// <summary>Only the merged artist has a picture; it is kept.</summary>
    Source,

    /// <summary>Neither artist has a picture.</summary>
    None,
}