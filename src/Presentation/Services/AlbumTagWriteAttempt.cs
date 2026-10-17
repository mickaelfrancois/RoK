using Rok.Application.Features.Albums.Edit;

namespace Rok.Services;

/// <summary>The result of one try to write the tags of an album, as the dialog needs it.</summary>
/// <param name="Outcome">The outcome reported by the write, null when the request itself failed.</param>
/// <param name="Error">The translated message to show when the request failed.</param>
public sealed record AlbumTagWriteAttempt(AlbumTagWriteOutcome? Outcome, string? Error)
{
    /// <summary>Builds the attempt of a write that ran, whatever its final state.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The attempt.</returns>
    public static AlbumTagWriteAttempt Success(AlbumTagWriteOutcome outcome) => new(outcome, null);

    /// <summary>Builds the attempt of a write that was refused or crashed.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The attempt.</returns>
    public static AlbumTagWriteAttempt Failure(string error) => new(null, error);
}