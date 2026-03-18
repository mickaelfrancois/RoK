namespace Rok.Services.PlayerCommand.Api;

public sealed class ListenGenreRouteHandler(IPlayerCommandService commandService, Action<Action> dispatch, ILogger<ListenGenreRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Prefix = "/listen/genre/";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path.StartsWith(Prefix, StringComparison.Ordinal);

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string genreName = Uri.UnescapeDataString(path[Prefix.Length..]);

        if (string.IsNullOrWhiteSpace(genreName))
            return WebApiResult.BadRequest();

        TaskCompletionSource<bool> tcs = new();

        dispatch(async () =>
        {
            try { tcs.SetResult(await commandService.ListenAlbumAsync(genreName)); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to listen genre {Name}", genreName);
                tcs.SetException(ex);
            }
        });

        return await tcs.Task
            ? WebApiResult.Ok()
            : WebApiResult.NotFound($"Genre '{genreName}' not found");
    }
}