using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>What the merge dialog calls to preview, run and search.</summary>
/// <param name="PreviewAsync">Tells what merging the first artist (merged) into the second (kept) would do.</param>
/// <param name="MergeAsync">Runs the merge and reports its progress; called again when the user retries.</param>
/// <param name="SearchAsync">Gives the artists whose name matches the typed text.</param>
public sealed record ArtistMergeDialogActions(
    Func<long, long, Task<ArtistMergePreviewAttempt>> PreviewAsync,
    Func<long, long, IProgress<ArtistRenameProgress>, Task<ArtistMergeAttempt>> MergeAsync,
    Func<string, Task<IReadOnlyList<ArtistMergeChoice>>> SearchAsync);