using Rok.Application.Features.Tracks.Services;

namespace Rok.Services;

/// <summary>Outcome of one save attempt of the track edit dialog.</summary>
/// <param name="Saved">The saved values, null when the save was refused.</param>
/// <param name="Error">The translated message to show when the save was refused.</param>
public sealed record TrackEditAttempt(TrackEditSaved? Saved, string? Error)
{
    /// <summary>Builds the outcome of a successful save.</summary>
    /// <param name="saved">The saved values.</param>
    /// <returns>The outcome.</returns>
    public static TrackEditAttempt Success(TrackEditSaved saved) => new(saved, null);

    /// <summary>Builds the outcome of a refused save.</summary>
    /// <param name="error">The translated message.</param>
    /// <returns>The outcome.</returns>
    public static TrackEditAttempt Failure(string error) => new(null, error);
}