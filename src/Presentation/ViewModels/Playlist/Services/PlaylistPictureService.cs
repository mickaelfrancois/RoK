using Rok.Application.Interfaces.Pictures;

namespace Rok.ViewModels.Playlist.Services;

public class PlaylistPictureService(IArtistPicture artistPicture, ILogger<PlaylistPictureService> logger)
{
    /// <summary>Returns <c>null</c> without a picture, so the picture control shows its themed placeholder.</summary>
    public BitmapImage? LoadPicture(string? pictureName)
    {
        try
        {
            if (!string.IsNullOrEmpty(pictureName) && artistPicture.PictureFileExists(pictureName))
            {
                string filePath = artistPicture.GetPictureFile(pictureName);
                return new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load picture for playlist: {PictureName}", pictureName);
            return null;
        }
    }

    public bool PictureExists(string artistName)
    {
        return artistPicture.PictureFileExists(artistName);
    }

    public string GetPictureFile(string artistName)
    {
        return artistPicture.GetPictureFile(artistName);
    }
}