using System.IO;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Maps the file extensions the web companion ships with onto their media types.
/// <c>.wasm</c> matters most: a browser refuses to stream-compile the Blazor runtime served as octet-stream.
/// </summary>
internal static class WebAssetContentType
{
    private const string Fallback = "application/octet-stream";

    public static string FromPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".webmanifest" => "application/manifest+json; charset=utf-8",
        ".wasm" => "application/wasm",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        ".ttf" => "font/ttf",
        ".txt" => "text/plain; charset=utf-8",
        ".map" => "application/json; charset=utf-8",
        ".dat" or ".blat" or ".pdb" or ".dll" => Fallback,
        _ => Fallback
    };
}