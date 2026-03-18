namespace Rok.Services.PlayerCommand.Api;

public sealed class ListenPlaylistRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch, ILogger<ListenPlaylistRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Prefix = "/listen/playlist/";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string playlistName = Uri.UnescapeDataString(path[Prefix.Length..]);

        if (string.IsNullOrWhiteSpace(playlistName))
            return WebApiResult.BadRequest();

        TaskCompletionSource<bool> tcs = new();

        dispatch(async () =>
        {
            try { tcs.SetResult(await commandService.ListenPlaylistAsync(playlistName)); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to listen playlist {Name}", playlistName);
                tcs.SetException(ex);
            }
        });

        return await tcs.Task
            ? WebApiResult.Ok()
            : WebApiResult.NotFound($"Playlist '{playlistName}' not found");
    }
}