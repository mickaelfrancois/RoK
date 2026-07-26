using Rok.Application.Interfaces.Pictures;
using Rok.Application.Player;

namespace Rok.Services.PlayerCommand.Api;

/// <summary>
/// Serves the raw artist image bytes of the track currently playing at <c>GET /current/artist-image</c>.
/// Returns 404 when nothing is playing (including radio) or when no artist image exists on disk.
/// </summary>
public sealed class CurrentArtistImageRouteHandler(
    IPlayerService playerService,
    IArtistPicture artistPicture,
    IFileSystem fileSystem,
    Action<Action> dispatch,
    ILogger<CurrentArtistImageRouteHandler> logger) : IWebApiRouteHandler
{
    private const string Route = "/current/artist-image";

    public bool CanHandle(string method, string path) =>
        method == "GET" && path == Route;

    public async Task<WebApiResult> HandleAsync(string path)
    {
        TrackDto? track = await ReadCurrentTrackAsync();

        if (track is null || string.IsNullOrEmpty(track.ArtistName))
            return WebApiResult.NotFound();

        if (!artistPicture.PictureFileExists(track.ArtistName))
            return WebApiResult.NotFound();

        string pictureFile = artistPicture.GetPictureFile(track.ArtistName);

        try
        {
            byte[] bytes = await fileSystem.ReadAllBytesAsync(pictureFile);
            return WebApiResult.Binary(bytes, ImageContentType.FromPath(pictureFile));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read artist image {File}", pictureFile);
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