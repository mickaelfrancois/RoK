using Rok.Application.Features.Artists.Merge;

namespace Rok.Services;

/// <summary>Result of one attempt to run the merge of two artists from the merge dialog.</summary>
/// <param name="Outcome">The outcome reported by the merge, null when the request itself failed.</param>
/// <param name="Error">The translated message to show when the request failed.</param>
public sealed record ArtistMergeAttempt(ArtistMergeOutcome? Outcome, string? Error)
{
    /// <summary>Builds the attempt of a merge that ran, whatever its final state.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The attempt.</returns>
    public static ArtistMergeAttempt Success(ArtistMergeOutcome outcome) => new(outcome, null);

    /// <summary>Builds the attempt of a merge that was refused or crashed.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The attempt.</returns>
    public static ArtistMergeAttempt Failure(string error) => new(null, error);
}

/// <summary>Result of one request for the preview of a merge.</summary>
/// <param name="Preview">What the merge would do, null when the request failed.</param>
/// <param name="Error">The translated message to show when the request failed.</param>
public sealed record ArtistMergePreviewAttempt(ArtistMergePreview? Preview, string? Error)
{
    /// <summary>Builds the attempt of a preview that was computed.</summary>
    /// <param name="preview">The preview.</param>
    /// <returns>The attempt.</returns>
    public static ArtistMergePreviewAttempt Success(ArtistMergePreview preview) => new(preview, null);

    /// <summary>Builds the attempt of a preview that was refused or crashed.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The attempt.</returns>
    public static ArtistMergePreviewAttempt Failure(string error) => new(null, error);
}