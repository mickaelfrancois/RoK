using System.IO;
using System.Net.Http;
using Rok.Application.Interfaces.Pictures;
using Rok.Services;
using Windows.Storage;

namespace Rok.ViewModels.Radio.Services;

public class RadioPictureService(
    IRadioPicture radioPicture,
    IHttpClientFactory httpClientFactory,
    ILogger<RadioPictureService> logger) : IRadioPictureService
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(8);

    public BitmapImage? LoadPicture(long stationId)
    {
        try
        {
            string filePath = radioPicture.GetPictureFile(stationId);

            if (!File.Exists(filePath))
                return null;

            return new BitmapImage(new Uri(filePath, UriKind.Absolute));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load cached picture for radio station {StationId}", stationId);
            return null;
        }
    }

    public async Task<BitmapImage?> SelectAndSavePictureAsync(long stationId)
    {
        StorageFile? file = await ImagePickerService.PickAsync();

        if (file is null)
            return null;

        try
        {
            string destinationPath = radioPicture.GetPictureFile(stationId);
            string? folderPath = Path.GetDirectoryName(destinationPath);

            if (string.IsNullOrEmpty(folderPath))
                return null;

            Directory.CreateDirectory(folderPath);
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);

            await file.CopyAsync(folder, Path.GetFileName(destinationPath), NameCollisionOption.ReplaceExisting);

            return await LoadPictureFromPathAsync(destinationPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save selected picture for radio station {StationId}", stationId);
            return null;
        }
    }

    public async Task<bool> DownloadAndSaveAsync(long stationId, string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return false;

        string destinationPath = radioPicture.GetPictureFile(stationId);

        try
        {
            using HttpClient http = httpClientFactory.CreateClient();
            http.Timeout = DownloadTimeout;

            using HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // The @Radios folder is created once by RadioPicture's ctor — no need to re-create it here.
            await using FileStream destination = File.Create(destinationPath);
            await response.Content.CopyToAsync(destination, cancellationToken);

            if (destination.Length == 0)
            {
                destination.Close();
                File.Delete(destinationPath);
                return false;
            }

            logger.LogDebug("Cached radio picture for station {StationId} from {Url}", stationId, url);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download picture for radio station {StationId} from {Url}", stationId, url);
            return false;
        }
    }

    public bool PictureExists(long stationId) => radioPicture.PictureFileExists(stationId);

    public string GetPictureFilePath(long stationId) => radioPicture.GetPictureFile(stationId);

    public Task DeletePictureAsync(long stationId, CancellationToken cancellationToken = default) =>
        radioPicture.DeletePictureFileAsync(stationId, cancellationToken);

    private static async Task<BitmapImage?> LoadPictureFromPathAsync(string path)
    {
        StorageFile sf = await StorageFile.GetFileFromPathAsync(path);
        using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await sf.OpenReadAsync();

        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);

        return bitmap;
    }
}
