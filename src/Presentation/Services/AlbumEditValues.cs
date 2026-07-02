namespace Rok.Services;

/// <summary>Immutable carrier for the album fields edited through the album edit dialog.</summary>
public sealed record AlbumEditValues
{
    public bool IsLive { get; init; }

    public bool IsBestOf { get; init; }

    public bool IsCompilation { get; init; }

    public bool IsLock { get; init; }

    public string? MusicBrainzID { get; init; }

    public string? ReleaseGroupMusicBrainzID { get; init; }

    public string? Biography { get; init; }

    public string? LastFmUrl { get; init; }
}