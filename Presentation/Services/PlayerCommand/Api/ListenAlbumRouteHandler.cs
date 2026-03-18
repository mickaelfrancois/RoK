namespace Rok.Services.PlayerCommand.Api;

public sealed class ListenAlbumRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch, ILogger<ListenAlbumRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Prefix = "/listen/album/";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string albumName = Uri.UnescapeDataString(path[Prefix.Length..]);

        if (string.IsNullOrWhiteSpace(albumName))
            return WebApiResult.BadRequest();

        TaskCompletionSource<bool> tcs = new();

        dispatch(async () =>
        {
            try { tcs.SetResult(await commandService.ListenAlbumAsync(albumName)); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to listen album {Name}", albumName);
                tcs.SetException(ex);
            }
        });

        return await tcs.Task
            ? WebApiResult.Ok()
            : WebApiResult.NotFound($"Album '{albumName}' not found");
    }
}