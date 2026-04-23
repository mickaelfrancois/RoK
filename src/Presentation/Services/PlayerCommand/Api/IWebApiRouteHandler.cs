namespace Rok.Services.PlayerCommand.Api;

public interface IWebApiRouteHandler
{
    bool CanHandle(string method, string path);

    Task<WebApiResult> HandleAsync(string path);
}