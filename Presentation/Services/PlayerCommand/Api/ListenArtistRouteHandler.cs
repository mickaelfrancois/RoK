namespace Rok.Services.PlayerCommand.Api;

public sealed class ListenArtistRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch, ILogger<ListenArtistRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Prefix = "/listen/artist/";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string artistName = Uri.UnescapeDataString(path[Prefix.Length..]);

        if (string.IsNullOrWhiteSpace(artistName))
            return WebApiResult.BadRequest();

        TaskCompletionSource<bool> tcs = new();

        dispatch(async () =>
        {
            try { tcs.SetResult(await commandService.ListenAlbumAsync(artistName)); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to listen artist {Name}", artistName);
                tcs.SetException(ex);
            }
        });

        return await tcs.Task
            ? WebApiResult.Ok()
            : WebApiResult.NotFound($"Artist '{artistName}' not found");
    }
}