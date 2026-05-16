namespace Rok.Application.Interfaces.Pictures;

public interface IAlbumPictureService
{
    bool PictureExists(string albumPath);
    string GetPictureFilePath(string albumPath);
}