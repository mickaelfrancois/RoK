using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>What the album merge dialog calls to preview, run and search.</summary>
/// <param name="PreviewAsync">Tells what merging the first album (merged) into the second (kept) would do.</param>
/// <param name="MergeAsync">Runs the merge, with the choice to shift the discs, and reports its progress; called again when the user retries.</param>
/// <param name="SearchAsync">Gives the albums whose name or artist matches the typed text.</param>
public sealed record AlbumMergeDialogActions(
    Func<long, long, Task<AlbumMergePreviewAttempt>> PreviewAsync,
    Func<long, long, bool, IProgress<ArtistRenameProgress>, Task<AlbumTagWriteAttempt>> MergeAsync,
    Func<string, Task<IReadOnlyList<AlbumMergeChoice>>> SearchAsync);

/// <summary>Result of one request for the preview of an album merge.</summary>
/// <param name="Preview">What the merge would do, null when the request failed.</param>
/// <param name="Error">The translated message to show when the request failed.</param>
public sealed record AlbumMergePreviewAttempt(AlbumMergePreview? Preview, string? Error)
{
    /// <summary>Builds the attempt of a preview that was computed.</summary>
    /// <param name="preview">The preview.</param>
    /// <returns>The attempt.</returns>
    public static AlbumMergePreviewAttempt Success(AlbumMergePreview preview) => new(preview, null);

    /// <summary>Builds the attempt of a preview that was refused or crashed.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The attempt.</returns>
    public static AlbumMergePreviewAttempt Failure(string error) => new(null, error);
}