namespace Rok.Application.Interfaces.Pictures;

public interface IBackdropPicture
{
    bool HasBackdrops(string artistName);
    string GetArtistPictureFolder(string artistName);
}