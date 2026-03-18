namespace Rok.Services.PlayerCommand.Api;

public readonly record struct WebApiResult(int StatusCode, string Body)
{
    public static WebApiResult Ok(string body = "") => new(200, body);

    public static WebApiResult NotFound() => new(404, string.Empty);

    public static WebApiResult NotFound(string body) => new(404, body);

    public static WebApiResult BadRequest() => new(400, string.Empty);
}