using System.IO;
using Rok.Application.Interfaces;
using Rok.Services;
using Windows.Storage;

namespace Rok.ViewModels.Album.Services;

public class AlbumPictureService(IAlbumPicture albumPicture, ILogger<AlbumPictureService> logger) : IAlbumPictureService
{
    public BitmapImage? LoadPicture(string albumPath)
    {
        try
        {
            if (albumPicture.PictureFileExists(albumPath))
            {
                string filePath = albumPicture.GetPictureFile(albumPath);
                return new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }
            else
            {
                return (BitmapImage)App.Current.Resources["albumCoverFallback"];
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load picture for album path: {AlbumPath}", albumPath);
            return (BitmapImage)App.Current.Resources["albumCoverFallback"];
        }
    }

    public async Task<BitmapImage?> SelectAndSavePictureAsync(string albumPath)
    {
        StorageFile? file = await ImagePickerService.PickAsync();
        if (file is null)
            return null;

        try
        {
            string destinationPath = albumPicture.GetPictureFile(albumPath);
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
            logger.LogError(ex, "Failed to save selected picture for album path: {AlbumPath}", albumPath);
            return null;
        }
    }

    public bool PictureExists(string albumPath)
    {
        return albumPicture.PictureFileExists(albumPath);
    }

    public string GetPictureFilePath(string albumPath)
    {
        return albumPicture.GetPictureFile(albumPath);
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