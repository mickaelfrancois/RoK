using System.IO;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Serves the raw album cover bytes of the track currently playing at <c>GET /current/album-cover</c>.
/// Returns 404 when nothing is playing (including radio) or when no cover file exists on disk.
/// </summary>
public sealed class CurrentAlbumCoverRouteHandler(
    IPlayerService playerService,
    IAlbumPicture albumPicture,
    IFileSystem fileSystem,
    Action<Action> dispatch,
    ILogger<CurrentAlbumCoverRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Route = "/current/album-cover";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path == Route;

    public async Task<WebApiResult> HandleAsync(string path)
    {
        TrackDto? track = await ReadCurrentTrackAsync();

        if (track is null || string.IsNullOrEmpty(track.MusicFile))
            return WebApiResult.NotFound();

        string? albumDirectory = Path.GetDirectoryName(track.MusicFile);

        if (string.IsNullOrEmpty(albumDirectory) || !albumPicture.PictureFileExists(albumDirectory))
            return WebApiResult.NotFound();

        string pictureFile = albumPicture.GetPictureFile(albumDirectory);

        try
        {
            byte[] bytes = await fileSystem.ReadAllBytesAsync(pictureFile);
            return WebApiResult.Binary(bytes, ImageContentType.FromPath(pictureFile));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read album cover {File}", pictureFile);
            return WebApiResult.NotFound();
        }
    }

    private Task<TrackDto?> ReadCurrentTrackAsync()
    {
        TaskCompletionSource<TrackDto?> tcs = new();

        dispatch(() =>
        {
            try
            {
                tcs.SetResult(playerService.CurrentTrack);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}