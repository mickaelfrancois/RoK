namespace Rok.Application.Interfaces;

public interface IArtistPicture
{
    void SetRepositoryArtistPath(string path);

    string GetArtistFolder(string artistName);

    string GetPictureFile(string artistName);

    bool PictureFileExists(string artistName);
}
