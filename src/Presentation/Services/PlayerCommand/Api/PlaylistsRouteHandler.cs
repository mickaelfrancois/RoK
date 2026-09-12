using System.Text.Json;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Features.Tracks.Requests;
using Rok.WebApi.Contracts;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Serves the playlist browsing routes of the web companion: <c>GET /api/playlists</c>,
/// <c>GET /api/playlists/{id}/tracks</c> and <c>POST /api/playlists/{id}/play</c>.
/// </summary>
/// <remarks>
/// The reads go through the UI thread like every other route: the application shares a single SQLite
/// connection, so querying straight from a listener thread would race with the queries the app itself runs.
/// </remarks>
public sealed class PlaylistsRouteHandler(IMediator mediator, IPlayerCommandService commandService, Action<Action> dispatch, ILogger<PlaylistsRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Root = "/api/playlists";
    private const string Prefix = "/api/playlists/";
    private const string TracksSuffix = "/tracks";
    private const string PlaySuffix = "/play";

    public bool CanHandle(string method, string path) =>
        (method == "GET" && (path == Root || (path.StartsWith(Prefix, StringComparison.Ordinal) && path.EndsWith(TracksSuffix, StringComparison.Ordinal))))
        || (method == "POST" && path.StartsWith(Prefix, StringComparison.Ordinal) && path.EndsWith(PlaySuffix, StringComparison.Ordinal));

    public async Task<WebApiResult> HandleAsync(string path)
    {
        try
        {
            if (path == Root)
                return WebApiResult.Ok(await ListPlaylistsAsync());

            if (path.EndsWith(TracksSuffix, StringComparison.Ordinal))
            {
                return TryReadId(path, TracksSuffix, out long id)
                    ? WebApiResult.Ok(await ListTracksAsync(id))
                    : WebApiResult.BadRequest();
            }

            if (!TryReadId(path, PlaySuffix, out long playlistId))
                return WebApiResult.BadRequest();

            return await UiDispatch.RunTaskAsync(dispatch, () => commandService.ListenPlaylistByIdAsync(playlistId))
                ? WebApiResult.Ok()
                : WebApiResult.NotFound($"Playlist {playlistId} is empty or unknown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to serve playlist route {Path}", path);
            throw;
        }
    }


    private async Task<string> ListPlaylistsAsync()
    {
        IEnumerable<PlaylistHeaderDto> playlists =
            await UiDispatch.RunTaskAsync(dispatch, () => mediator.Send(new GetAllPlaylistsRequest()));

        return JsonSerializer.Serialize(playlists.Select(WebApiMapper.ToPlaylistSummary), RokJson.Options);
    }


    private async Task<string> ListTracksAsync(long playlistId)
    {
        IEnumerable<TrackDto> tracks =
            await UiDispatch.RunTaskAsync(dispatch, () => mediator.Send(new GetTracksByPlaylistIdRequest(playlistId)));

        return JsonSerializer.Serialize(tracks.Select(WebApiMapper.ToLibraryTrack), RokJson.Options);
    }


    private static bool TryReadId(string path, string suffix, out long id) =>
        long.TryParse(path[Prefix.Length..^suffix.Length], out id);
}