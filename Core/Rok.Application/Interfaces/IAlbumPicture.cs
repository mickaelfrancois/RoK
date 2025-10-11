namespace Rok.Application.Interfaces;

public interface IAlbumPicture
{
    bool PictureFileExists(string albumPath);

    string GetPictureFile(string albumPath);
}
