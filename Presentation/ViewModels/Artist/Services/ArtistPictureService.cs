using System.IO;
using Rok.Services;
using Windows.Storage;

namespace Rok.ViewModels.Artist.Services;

public class ArtistPictureService(IArtistPicture artistPicture, ILogger<ArtistPictureService> logger) : IArtistPictureService
{
    private static BitmapImage FallbackPicture => new(new Uri("ms-appx:///Assets/artistFallback.png"));

    public BitmapImage LoadPicture(string artistName)
    {
        try
        {
            if (artistPicture.PictureFileExists(artistName))
            {
                string filePath = artistPicture.GetPictureFile(artistName);
                return new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                return FallbackPicture;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load picture for artist: {ArtistName}", artistName);
            return FallbackPicture;
        }
    }

    public async Task<BitmapImage?> SelectAndSavePictureAsync(string artistName)
    {
        StorageFile? file = await ImagePickerService.PickAsync();
        if (file is null)
            return null;

        try
        {
            string destinationPath = artistPicture.GetPictureFile(artistName);
            string? folderPath = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);

                await file.CopyAsync(folder, Path.GetFileName(destinationPath), NameCollisionOption.ReplaceExisting);
            }

            return await LoadPictureFromPathAsync(destinationPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save selected picture for artist: {ArtistName}", artistName);
            return null;
        }
    }

    public bool PictureExists(string artistName)
    {
        return artistPicture.PictureFileExists(artistName);
    }

    public string GetPictureFilePath(string artistName)
    {
        return artistPicture.GetPictureFile(artistName);
    }

    private static async Task<BitmapImage?> LoadPictureFromPathAsync(string path)
    {
        StorageFile sf = await StorageFile.GetFileFromPathAsync(path);
        using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await sf.OpenReadAsync();

        BitmapImage bitmap = new();
        await bitmap.SetSourceAsync(stream);

        return bitmap;
    }
}