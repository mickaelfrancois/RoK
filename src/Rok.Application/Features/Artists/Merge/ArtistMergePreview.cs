using Rok.Application.Features.Artists.Rename;

namespace Rok.Application.Features.Artists.Merge;

/// <summary>
/// What merging two artists would do, shown before the user confirms.
/// </summary>
/// <param name="SourceId">Id of the artist that disappears.</param>
/// <param name="SourceName">Name of the artist that disappears.</param>
/// <param name="TargetId">Id of the artist that stays.</param>
/// <param name="TargetName">Name of the artist that stays; the files are rewritten with it.</param>
/// <param name="FileCount">Number of track files of the merged artist.</param>
/// <param name="AlbumCount">Number of distinct albums of these files.</param>
/// <param name="MergedAlbums">Homonym albums that become one.</param>
/// <param name="Conflicts">Fields filled on both artists with different values; the kept artist's value wins.</param>
/// <param name="Blockers">Reasons the merge cannot start. Empty when it can.</param>
/// <param name="Playlists">Smart playlists whose result can change.</param>
/// <param name="Picture">Which artist picture survives.</param>
public sealed record ArtistMergePreview(
    long SourceId,
    string SourceName,
    long TargetId,
    string TargetName,
    int FileCount,
    int AlbumCount,
    IReadOnlyList<AlbumMergeGroupPreview> MergedAlbums,
    IReadOnlyList<ArtistMergeFieldConflict> Conflicts,
    IReadOnlyList<ArtistRenameBlocker> Blockers,
    IReadOnlyList<SmartPlaylistMergeImpact> Playlists,
    EArtistMergePicture Picture);