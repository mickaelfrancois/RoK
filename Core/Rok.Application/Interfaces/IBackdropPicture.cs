namespace Rok.Application.Interfaces;

public interface IBackdropPicture
{
    bool HasBackdrops(string artistName);
    string GetArtistPictureFolder(string artistName);
}
