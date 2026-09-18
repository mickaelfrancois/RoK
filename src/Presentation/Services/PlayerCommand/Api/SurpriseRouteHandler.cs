using System.Text.Json;
using Rok.WebApi.Contracts;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Draws and plays a random album or artist at <c>POST /api/surprise/album</c> and
/// <c>POST /api/surprise/artist</c>, answering with what was drawn so the companion can name it.
/// </summary>
public sealed class SurpriseRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch) : IWebApiRouteHandler
{
    private const string AlbumRoute = "/api/surprise/album";
    private const string ArtistRoute = "/api/surprise/artist";

    public bool CanHandle(string method, string path) =>
        method == "POST" && path is AlbumRoute or ArtistRoute;

    public async Task<WebApiResult> HandleAsync(string path)
    {
        SurprisePick? pick = path == AlbumRoute
            ? await UiDispatch.RunTaskAsync(dispatch, commandService.SurpriseAlbumAsync)
            : await UiDispatch.RunTaskAsync(dispatch, commandService.SurpriseArtistAsync);

        return pick is null
            ? WebApiResult.NotFound("The library holds nothing playable to draw from")
            : WebApiResult.Ok(JsonSerializer.Serialize(pick, RokJsonContext.Default.SurprisePick));
    }
}