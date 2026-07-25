using System.Text.Json;
using Rok.Application.Features.Playlists.Requests;

namespace Rok.Application.Features.Playlists;

/// <summary>
/// Decides whether a playlist update command actually differs from the persisted
/// state, field by field, so that PlaylistUpdateService only writes on real changes.
/// </summary>
public static class PlaylistHeaderChangeDetector
{
    public static bool HasChanges(UpdatePlaylistRequest command, PlaylistHeaderDto persisted)
    {
        if (command.Name != persisted.Name)
            return true;

        if (command.Duration != persisted.Duration)
            return true;

        if (command.TrackCount != persisted.TrackCount)
            return true;

        if (command.TrackMaximum != persisted.TrackMaximum)
            return true;

        if (command.DurationMaximum != persisted.DurationMaximum)
            return true;

        if ((command.Picture ?? string.Empty) != (persisted.Picture ?? string.Empty))
            return true;

        if (command.ShuffleOnPlay != persisted.ShuffleOnPlay)
            return true;

        if (command.RefreshOnPlay != persisted.RefreshOnPlay)
            return true;

        string commandGroupsJson = command.Groups is { Count: > 0 } ? JsonSerializer.Serialize(command.Groups) : string.Empty;
        if (commandGroupsJson != (persisted.GroupsJson ?? string.Empty))
            return true;

        return false;
    }
}