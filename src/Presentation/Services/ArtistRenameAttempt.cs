using Rok.Application.Features.Artists.Rename;

namespace Rok.Services;

/// <summary>Result of one attempt to run the rename of an artist from the rename dialog.</summary>
/// <param name="Outcome">The outcome reported by the rename, null when the request itself failed.</param>
/// <param name="Error">The translated message to show when the request failed.</param>
public sealed record ArtistRenameAttempt(ArtistRenameOutcome? Outcome, string? Error)
{
    /// <summary>Builds the attempt of a rename that ran, whatever its final state.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The attempt.</returns>
    public static ArtistRenameAttempt Success(ArtistRenameOutcome outcome) => new(outcome, null);

    /// <summary>Builds the attempt of a rename that was refused or crashed.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The attempt.</returns>
    public static ArtistRenameAttempt Failure(string error) => new(null, error);
}