using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Rok.Application.Player;

namespace Rok.Services;

public sealed partial class PlayerWebApiService(IPlayerService playerService, IAppOptions options, Action<Action> dispatch, ILogger<PlayerWebApiService> logger) : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public int ActivePort { get; private set; }


    public void Start()
    {
        try
        {
            ActivePort = ResolveAvailablePort(options.WebApiPort);

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{ActivePort}/");
            _listener.Start();

            logger.LogInformation("Player Web API started on http://localhost:{Port}", ActivePort);

            Task.Run(() => ListenAsync(_cts.Token), _cts.Token);
        }
        catch (HttpListenerException ex)
        {
            logger.LogError(ex, "Failed to start Player Web API on port {Port}", ActivePort);
            Stop();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while starting Player Web API");
            Stop();
        }
    }


    private static int ResolveAvailablePort(int preferredPort)
    {
        try
        {
            TcpListener probe = new(IPAddress.Loopback, preferredPort);
            probe.Start();
            probe.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            TcpListener probe = new(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }


    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
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
            string path = request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? string.Empty;

            string? json = path switch
            {
                "/status" => await DispatchAsync(BuildStatusJson),
                "/current" => await DispatchAsync(BuildCurrentJson),
                "/queue" => await DispatchAsync(BuildQueueJson),
                "/play" => await DispatchVoidAsync(playerService.Play),
                "/pause" => await DispatchVoidAsync(playerService.Pause),
                "/toggle" => await DispatchAsync(HandleToggle),
                "/next" => await DispatchVoidAsync(playerService.Skip),
                "/previous" => await DispatchVoidAsync(playerService.Previous),
                "/mute" => await DispatchVoidAsync(() => playerService.IsMuted = !playerService.IsMuted),
                _ => null
            };

            if (path.StartsWith("/volume/", StringComparison.Ordinal)
                && double.TryParse(path["/volume/".Length..], out double volume))
            {
                json = await DispatchVoidAsync(() => playerService.Volume = Math.Clamp(volume, 0, 100));
            }

            if (json is null)
            {
                response.StatusCode = 404;
                return;
            }

            response.ContentType = "application/json";
            response.StatusCode = 200;

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer);
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

    private string HandleToggle()
    {
        if (playerService.PlaybackState == EPlaybackState.Playing)
            playerService.Pause();
        else
            playerService.Play();

        return string.Empty;
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