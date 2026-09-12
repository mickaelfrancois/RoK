namespace Rok.WebApi.Contracts;

/// <summary>
/// A playlist as listed by <c>GET /api/playlists</c>.
/// </summary>
/// <param name="Id">The playlist identifier.</param>
/// <param name="Name">The playlist name.</param>
/// <param name="TrackCount">The number of tracks the playlist holds.</param>
/// <param name="Duration">The total duration of the playlist, in seconds.</param>
/// <param name="IsSmart">Whether the playlist is a smart (rule-based) playlist.</param>
/// <param name="ShuffleOnPlay">Whether the playlist is shuffled when it starts playing.</param>
public sealed record PlaylistSummary(
    long Id,
    string Name,
    int TrackCount,
    long Duration,
    bool IsSmart,
    bool ShuffleOnPlay);