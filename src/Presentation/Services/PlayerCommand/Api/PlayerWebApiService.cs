using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Rok.Application.Player;
using Rok.Services.PlayerCommand;
using Rok.Services.PlayerCommand.Api;

namespace Rok.Services;

/// <summary>
/// Provides a web API service for controlling and monitoring player operations, such as playback commands and status
/// retrieval, over HTTP.
/// </summary>
/// <remarks>This service hosts an HTTP listener that exposes endpoints for player control and status queries. It
/// automatically selects an available port if the preferred port is unavailable. The service is thread-safe and should
/// be disposed of when no longer needed to release resources.</remarks>
/// <param name="playerService">The service that manages the state and operations of the player, including playback status, current track, and queue
/// information.</param>
/// <param name="options">The application configuration options that specify settings for the web API, including the preferred port to listen
/// on.</param>
/// <param name="dispatch">An action used to dispatch operations to the appropriate thread, ensuring thread safety for player-related actions.</param>
/// <param name="routeHandlers">A collection of route handlers that process custom or extended API routes beyond the built-in player commands.</param>
/// <param name="commandService">The service responsible for executing player commands, such as play, pause, next, and volume adjustments.</param>
/// <param name="logger">A logger instance used to record informational messages and errors related to the operation of the web API service.</param>
public sealed partial class PlayerWebApiService(
    IPlayerService playerService,
    IAppOptions options,
    Action<Action> dispatch,
    IEnumerable<IWebApiRouteHandler> routeHandlers,
    IPlayerCommandService commandService,
    ILogger<PlayerWebApiService> logger) : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public int ActivePort { get; private set; }


    public void Start()
    {
        try
        {
            bool allowLan = options.WebApiAllowLan;

            ActivePort = ResolveAvailablePort(options.WebApiPort, allowLan);
            _cts = new CancellationTokenSource();

            if (allowLan && TryStartListener($"http://+:{ActivePort}/"))
            {
                logger.LogInformation("Player Web API started on http://{Machine}:{Port} (reachable from the local network)",
                    Environment.MachineName, ActivePort);
            }
            else
            {
                if (allowLan)
                {
                    logger.LogWarning(
                        "Player Web API could not bind every interface; falling back to loopback. Grant the reservation once from an elevated prompt: netsh http add urlacl url=http://+:{Port}/ user={User}",
                        ActivePort, Environment.UserName);
                }

                if (!TryStartListener($"http://localhost:{ActivePort}/"))
                {
                    logger.LogError("Failed to start Player Web API on port {Port}", ActivePort);
                    Stop();
                    return;
                }

                logger.LogInformation("Player Web API started on http://localhost:{Port}", ActivePort);
            }

            _ = Task.Run(() => ListenAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while starting Player Web API");
            Stop();
        }
    }


    private bool TryStartListener(string prefix)
    {
        HttpListener listener = new();

        try
        {
            listener.Prefixes.Add(prefix);
            listener.Start();
            _listener = listener;
            return true;
        }
        catch (HttpListenerException ex)
        {
            logger.LogDebug(ex, "Could not bind the Player Web API to {Prefix}", prefix);
            listener.Close();
            return false;
        }
    }


    private static int ResolveAvailablePort(int preferredPort, bool allowLan)
    {
        IPAddress probeAddress = allowLan ? IPAddress.Any : IPAddress.Loopback;

        try
        {
            TcpListener probe = new(probeAddress, preferredPort);
            probe.Start();
            probe.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            TcpListener probe = new(probeAddress, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }


    public void Stop()
    {
        if (_disposed)
            return;

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException)
        {
        }
    }


    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && (_listener?.IsListening ?? false))
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Player Web API listener error");
            }
        }
    }


    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            ApplyCorsHeaders(request, response);

            string path = request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? string.Empty;
            string method = request.HttpMethod.ToUpperInvariant();

            if (method == "OPTIONS")
            {
                response.StatusCode = 204;
                return;
            }

            if (WebApiOriginGuard.IsStateChanging(method, path)
                && WebApiOriginGuard.IsCrossSite(request.Headers["Origin"], request.Headers["Sec-Fetch-Site"]))
            {
                logger.LogWarning("Refused a cross-site {Method} {Path} from origin {Origin}",
                    method, path, request.Headers["Origin"] ?? "(none)");

                response.StatusCode = 403;
                return;
            }

            WebApiResult result = await ResolveAsync(method, path);

            response.StatusCode = result.StatusCode;

            if (result.BinaryBody is { } binaryBody)
            {
                response.ContentType = result.ContentType ?? "application/octet-stream";
                response.ContentLength64 = binaryBody.Length;
                await response.OutputStream.WriteAsync(binaryBody);
            }
            else
            {
                response.ContentType = "application/json";

                if (!string.IsNullOrEmpty(result.Body))
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(result.Body);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling {Method} {Path}", request.HttpMethod, request.Url?.AbsolutePath);
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    /// <summary>
    /// Echoes the caller origin back when it belongs to this machine or a private network, so that the companion
    /// served from a development server can call the API. Public origins are deliberately never allowed: with a
    /// wildcard, any web page the user happens to visit could read the library through their browser.
    /// </summary>
    private static void ApplyCorsHeaders(HttpListenerRequest request, HttpListenerResponse response)
    {
        string? origin = request.Headers["Origin"];

        if (string.IsNullOrEmpty(origin) || !WebApiOriginGuard.IsLocalOrigin(origin))
            return;

        response.AddHeader("Access-Control-Allow-Origin", origin);
        response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
        response.AddHeader("Vary", "Origin");
    }


    private async Task<WebApiResult> ResolveAsync(string method, string path)
    {
        string? json = path switch
        {
            "/status" => await DispatchAsync(BuildStatusJson),
            "/current" => await DispatchAsync(BuildCurrentJson),
            "/queue" => await DispatchAsync(BuildQueueJson),
            "/play" => await DispatchVoidAsync(commandService.Play),
            "/pause" => await DispatchVoidAsync(commandService.Pause),
            "/toggle" => await DispatchVoidAsync(commandService.Toggle),
            "/next" => await DispatchVoidAsync(commandService.Next),
            "/previous" => await DispatchVoidAsync(commandService.Previous),
            "/mute" => await DispatchVoidAsync(commandService.ToggleMute),
            _ => null
        };

        if (json is not null)
            return WebApiResult.Ok(json);

        if (path.StartsWith("/volume/", StringComparison.Ordinal)
            && double.TryParse(path["/volume/".Length..], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double volume))
        {
            await DispatchVoidAsync(() => commandService.SetVolume(volume));
            return WebApiResult.Ok();
        }

        IWebApiRouteHandler? handler = routeHandlers.FirstOrDefault(h => h.CanHandle(method, path));
        if (handler is not null)
            return await handler.HandleAsync(path);

        return WebApiResult.NotFound();
    }


    private Task<string> DispatchAsync(Func<string> action)
    {
        TaskCompletionSource<string> tcs = new();
        dispatch(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private Task<string> DispatchVoidAsync(Action action) =>
        DispatchAsync(() => { action(); return string.Empty; });


    private string BuildStatusJson()
    {
        return JsonSerializer.Serialize(new
        {
            state = playerService.PlaybackState.ToString(),
            volume = playerService.Volume,
            position = playerService.Position,
            isMuted = playerService.IsMuted,
            canNext = playerService.CanNext,
            canPrevious = playerService.CanPrevious,
        });
    }

    private string BuildCurrentJson()
    {
        TrackDto? track = playerService.CurrentTrack;
        return JsonSerializer.Serialize(new
        {
            title = track?.Title,
            artist = track?.ArtistName,
            album = track?.AlbumName,
            genre = track?.GenreName,
            score = track?.Score,
            isFavoriteArtist = track?.IsArtistFavorite,
            isFavoriteAlbum = track?.IsAlbumFavorite,
            isFavoriteGenre = track?.IsGenreFavorite
        });
    }

    private string BuildQueueJson()
    {
        return JsonSerializer.Serialize(playerService.GetQueue()
                                    .Select(t => new
                                    {
                                        title = t.Title,
                                        artist = t.ArtistName,
                                        album = t.AlbumName,
                                        genre = t.GenreName,
                                        score = t.Score,
                                        isFavoriteArtist = t.IsArtistFavorite,
                                        isFavoriteAlbum = t.IsAlbumFavorite,
                                        isFavoriteGenre = t.IsGenreFavorite
                                    }));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _cts?.Dispose();
        _listener?.Close();
        _disposed = true;
    }
}