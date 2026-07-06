namespace Rok.Services;

/// <summary>Immutable carrier for the artist fields edited through the artist edit dialog.</summary>
/// <remarks>
/// Year fields are carried as <see langword="string"/> so the dialog binds them to plain
/// <c>TextBox</c> controls (mirroring the album dialog); <see cref="ArtistEditService"/> parses
/// them back to <see cref="int"/> when building the update command. An empty or non-numeric
/// year maps to <see langword="null"/>.
/// </remarks>
public sealed record ArtistEditValues
{
    public string? MusicBrainzID { get; init; }

    public string? FormedYear { get; init; }

    public string? BornYear { get; init; }

    public string? DiedYear { get; init; }

    public bool Disbanded { get; init; }

    public string? Members { get; init; }

    public string? Biography { get; init; }
}