namespace Rok.WebApi.Contracts;

/// <summary>
/// Snapshot of the player state returned by <c>GET /api/player/status</c>.
/// </summary>
/// <param name="State">The playback state name (for example <c>Playing</c>, <c>Paused</c>, <c>Stopped</c>).</param>
/// <param name="Mode">The playback mode name (for example <c>Library</c>, <c>Radio</c>).</param>
/// <param name="Volume">The current volume, from 0 to 100.</param>
/// <param name="IsMuted">Whether the output is muted.</param>
/// <param name="Position">The position within the current track, in seconds.</param>
/// <param name="CanNext">Whether a next track is available.</param>
/// <param name="CanPrevious">Whether a previous track is available.</param>
/// <param name="CanSeek">Whether the current source supports seeking.</param>
/// <param name="IsLooping">Whether looping is enabled.</param>
/// <param name="IsBuffering">Whether the engine is currently buffering.</param>
/// <param name="QueueLength">The number of tracks currently loaded in the queue.</param>
/// <param name="QueueSignature">
/// Fingerprint of the queue content, its order and the playhead position. A client can compare it between
/// two polls to know whether the queue is worth fetching again. Shuffling changes neither the length nor the
/// playing track, so without it a reordered queue would go unnoticed.
/// </param>
/// <param name="Current">The track being played, or <c>null</c> when nothing is loaded.</param>
public sealed record PlayerStatus(
    string State,
    string Mode,
    double Volume,
    bool IsMuted,
    double Position,
    bool CanNext,
    bool CanPrevious,
    bool CanSeek,
    bool IsLooping,
    bool IsBuffering,
    int QueueLength,
    long QueueSignature,
    NowPlaying? Current);