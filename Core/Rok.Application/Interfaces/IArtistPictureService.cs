namespace Rok.Application.Interfaces;

public interface IArtistPictureService
{
    bool PictureExists(string artistName);
    string GetPictureFilePath(string artistName);
}
