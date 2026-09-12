using System.IO;
using Rok.Application.Interfaces;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Serves the published web companion as static files, so that the browser loads the UI from the same origin
/// as the API and no cross-origin configuration is needed.
/// </summary>
/// <remarks>
/// The assets deliberately live outside the application package, under the writable local folder, so that the
/// companion is never shipped to the Store alongside Rok. This handler must stay registered last: it answers a
/// very broad set of paths and would otherwise shadow the API routes.
/// <para>
/// Request paths reach handlers already lower-cased, while the published Blazor assets carry mixed-case
/// fingerprinted names. That is harmless here because Rok is Windows-only and its file system resolves names
/// case-insensitively; the bytes served, and therefore the integrity hashes the browser checks, are unaffected.
/// </para>
/// </remarks>
/// <param name="defaultRoot">Folder used when no explicit root is configured in the options.</param>
public sealed class WebAppRouteHandler(string defaultRoot, IAppOptions options, IFileSystem fileSystem, ILogger<WebAppRouteHandler> logger) : IWebApiRouteHandler
{
    private const string EntryPoint = "index.html";

    public bool CanHandle(string method, string path) =>
        method == "GET"
        && !path.StartsWith("/api", StringComparison.Ordinal)
        && fileSystem.DirectoryExists(ResolveRoot());

    public async Task<WebApiResult> HandleAsync(string path)
    {
        string? file = ResolveFile(ResolveRoot(), path);

        if (file is null)
            return WebApiResult.NotFound();

        try
        {
            byte[] content = await fileSystem.ReadAllBytesAsync(file);

            return WebApiResult.Binary(content, WebAssetContentType.FromPath(file));
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Failed to read web companion asset {File}", file);
            return WebApiResult.NotFound();
        }
    }


    private string ResolveRoot() =>
        string.IsNullOrWhiteSpace(options.WebAppRoot) ? defaultRoot : options.WebAppRoot;


    /// <summary>
    /// Maps a request path onto a file inside the companion folder, refusing anything that escapes it.
    /// An unknown extensionless path falls back to the entry point so that client-side routing keeps working
    /// when a deep link is opened or reloaded.
    /// </summary>
    private string? ResolveFile(string root, string path)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string relative = path.TrimStart('/');

        if (relative.Length == 0)
            relative = EntryPoint;

        string candidate;

        try
        {
            candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        if (fileSystem.FileExists(candidate))
            return candidate;

        if (Path.HasExtension(candidate))
            return null;

        string entryPoint = Path.Combine(normalizedRoot, EntryPoint);

        return fileSystem.FileExists(entryPoint) ? entryPoint : null;
    }
}