namespace Rok.Application.Interfaces;

public interface IAlbumPictureService
{
    bool PictureExists(string albumPath);
    string GetPictureFilePath(string albumPath);
}
