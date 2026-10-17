namespace Rok.Services;

/// <summary>Immutable carrier for the album fields edited through the album edit dialog.</summary>
public sealed record AlbumEditValues
{
    /// <summary>Gets the album name typed in the dialog. It is only used when the tagged fields are shown.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the artist name typed in the dialog. It is only used when the tagged fields are editable.</summary>
    public string? ArtistName { get; init; }

    /// <summary>Gets the year typed in the dialog. Empty keeps the current year.</summary>
    public string? Year { get; init; }

    /// <summary>Gets the genre name typed in the dialog. Empty keeps the current genre.</summary>
    public string? GenreName { get; init; }

    /// <summary>Gets a value indicating whether the genre cannot be edited because the import gives each track the genre of its artist.</summary>
    public bool GenreLocked { get; init; }

    public bool IsLive { get; init; }

    public bool IsBestOf { get; init; }

    public bool IsCompilation { get; init; }

    public bool IsLock { get; init; }

    public string? MusicBrainzID { get; init; }

    public string? ReleaseGroupMusicBrainzID { get; init; }

    public string? Biography { get; init; }

    public string? LastFmUrl { get; init; }
}