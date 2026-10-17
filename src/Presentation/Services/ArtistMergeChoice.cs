namespace Rok.Services;

/// <summary>An artist the user can pick in the merge dialog.</summary>
/// <param name="Id">Id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
public sealed record ArtistMergeChoice(long Id, string Name)
{
    /// <summary>Gives the name, which the suggestion box shows.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Name;
}