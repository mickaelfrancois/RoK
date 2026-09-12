using System.Text.Json;
using Rok.Application.Player;
using Rok.WebApi.Contracts;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Serves the player state read by the web companion: <c>GET /api/player/status</c> and
/// <c>GET /api/player/queue</c>. Both answer with the shared <c>Rok.WebApi.Contracts</c> shapes.
/// </summary>
public sealed class PlayerStatusRouteHandler(IPlayerService playerService, Action<Action> dispatch) : IWebApiRouteHandler
{
    private const string StatusRoute = "/api/player/status";
    private const string QueueRoute = "/api/player/queue";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path is StatusRoute or QueueRoute;

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string json = path == QueueRoute
            ? await UiDispatch.ReadAsync(dispatch, () => JsonSerializer.Serialize(WebApiMapper.ToQueue(playerService), RokJson.Options))
            : await UiDispatch.ReadAsync(dispatch, () => JsonSerializer.Serialize(WebApiMapper.ToStatus(playerService), RokJson.Options));

        return WebApiResult.Ok(json);
    }
}