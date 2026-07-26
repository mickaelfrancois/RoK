namespace Rok.Services.PlayerCommand.Api;

public readonly record struct WebApiResult(int StatusCode, string Body)
{
    /// <summary>
    /// Raw binary payload to write to the response, or <c>null</c> for a text/JSON response.
    /// </summary>
    public byte[]? BinaryBody { get; init; }

    /// <summary>
    /// The <c>Content-Type</c> to send with a binary payload; ignored for text/JSON responses.
    /// </summary>
    public string? ContentType { get; init; }

    public static WebApiResult Ok(string body = "") => new(200, body);

    public static WebApiResult NotFound() => new(404, string.Empty);

    public static WebApiResult NotFound(string body) => new(404, body);

    public static WebApiResult BadRequest() => new(400, string.Empty);

    /// <summary>
    /// Creates a 200 response carrying raw bytes with the given content type (e.g. an image).
    /// </summary>
    /// <param name="content">The raw bytes to write to the response body.</param>
    /// <param name="contentType">The MIME content type to advertise (e.g. <c>image/jpeg</c>).</param>
    public static WebApiResult Binary(byte[] content, string contentType) =>
        new(200, string.Empty) { BinaryBody = content, ContentType = contentType };
}