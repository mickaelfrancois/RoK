namespace Rok.Logic.ViewModels.Playlist.Services;

public class PlaylistPictureService(IArtistPicture artistPicture, ILogger<PlaylistPictureService> logger)
{
    private static BitmapImage FallbackPicture => new(new Uri("ms-appx:///Assets/artistFallback.png"));

    public BitmapImage LoadPicture(string? pictureName)
    {
        try
        {
            if (!string.IsNullOrEmpty(pictureName) && artistPicture.PictureFileExists(pictureName))
            {
                string filePath = artistPicture.GetPictureFile(pictureName);
                return new BitmapImage(new Uri(filePath, UriKind.Absolute));
            }

            return FallbackPicture;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load picture for playlist: {PictureName}", pictureName);
            return FallbackPicture;
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