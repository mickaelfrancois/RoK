namespace Rok.Application.Interfaces.Pictures;

public interface IArtistPictureService
{
    bool PictureExists(string artistName);
    string GetPictureFilePath(string artistName);
}
