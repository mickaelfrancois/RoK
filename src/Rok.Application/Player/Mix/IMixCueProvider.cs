using Rok.Application.Dto;

namespace Rok.Application.Player.Mix;

/// <summary>Provides the Mix mode cues of a track.</summary>
public interface IMixCueProvider
{
    /// <summary>Finds where the music ends and whether a natural fade-out closes the track.</summary>
    /// <param name="track">Track to analyse.</param>
    /// <param name="ct">Cancellation token; a cancelled analysis yields <c>null</c>.</param>
    /// <returns>The cues, or <c>null</c> when they cannot be determined. Never throws.</returns>
    Task<OutroCues?> GetOutroAsync(TrackDto track, CancellationToken ct);

    /// <summary>Finds where the music starts in a track.</summary>
    /// <param name="track">Track to analyse.</param>
    /// <param name="ct">Cancellation token; a cancelled analysis yields <c>null</c>.</param>
    /// <returns>The cues, or <c>null</c> when they cannot be determined. Never throws.</returns>
    Task<IntroCues?> GetIntroAsync(TrackDto track, CancellationToken ct);
}