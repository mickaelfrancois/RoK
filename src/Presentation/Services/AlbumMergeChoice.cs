namespace Rok.Services;

/// <summary>An album the user can pick in the merge dialog.</summary>
/// <param name="Id">Id of the album.</param>
/// <param name="Name">Name of the album.</param>
/// <param name="ArtistName">Name of the artist of the album; two albums can share a name.</param>
public sealed record AlbumMergeChoice(long Id, string Name, string ArtistName)
{
    /// <summary>Gets the text the suggestion box and the radio buttons show: the name, then the artist when there is one.</summary>
    public string Label => string.IsNullOrEmpty(ArtistName) ? Name : $"{Name} — {ArtistName}";

    /// <summary>Gives the label, which the suggestion box shows.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}