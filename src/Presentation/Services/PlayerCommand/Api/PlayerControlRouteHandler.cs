using System.Globalization;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Executes the player commands issued by the web companion under <c>POST /api/player/…</c>.
/// The verb is POST on purpose: the legacy GET routes stay for the terminal companion, but a browser
/// prefetching a link must never be able to skip a track.
/// </summary>
public sealed class PlayerControlRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch) : IWebApiRouteHandler
{
    private const string Prefix = "/api/player/";
    private const string VolumePrefix = "volume/";
    private const string SeekPrefix = "seek/";
    private const string QueuePrefix = "queue/";
    private const string PlaySuffix = "/play";

    public bool CanHandle(string method, string path) =>
        method == "POST" && path.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string command = path[Prefix.Length..];

        Action? playerCommand = command switch
        {
            "play" => commandService.Play,
            "pause" => commandService.Pause,
            "toggle" => commandService.Toggle,
            "next" => commandService.Next,
            "previous" => commandService.Previous,
            "mute" => commandService.ToggleMute,
            "shuffle" => commandService.Shuffle,
            "loop" => commandService.ToggleLoop,
            _ => null
        };

        if (playerCommand is not null)
        {
            await UiDispatch.InvokeAsync(dispatch, playerCommand);
            return WebApiResult.Ok();
        }

        if (TryReadNumber(command, VolumePrefix, out double volume))
        {
            await UiDispatch.InvokeAsync(dispatch, () => commandService.SetVolume(volume));
            return WebApiResult.Ok();
        }

        if (TryReadNumber(command, SeekPrefix, out double position))
        {
            await UiDispatch.InvokeAsync(dispatch, () => commandService.Seek(position));
            return WebApiResult.Ok();
        }

        if (TryReadQueuedTrackId(command, out long trackId))
        {
            return await UiDispatch.ReadAsync(dispatch, () => commandService.PlayQueuedTrack(trackId))
                ? WebApiResult.Ok()
                : WebApiResult.NotFound($"Track {trackId} is not queued");
        }

        return WebApiResult.NotFound();
    }


    private static bool TryReadNumber(string command, string prefix, out double value)
    {
        value = 0;

        return command.StartsWith(prefix, StringComparison.Ordinal)
            && double.TryParse(command[prefix.Length..], NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }


    private static bool TryReadQueuedTrackId(string command, out long trackId)
    {
        trackId = 0;

        if (!command.StartsWith(QueuePrefix, StringComparison.Ordinal) || !command.EndsWith(PlaySuffix, StringComparison.Ordinal))
            return false;

        return long.TryParse(command[QueuePrefix.Length..^PlaySuffix.Length], out trackId);
    }
}