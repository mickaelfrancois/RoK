namespace Rok.Application.Interfaces.Pictures;

public interface IAlbumPicture
{
    bool PictureFileExists(string albumPath);

    string GetPictureFile(string albumPath);
}