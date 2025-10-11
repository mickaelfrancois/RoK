namespace Rok.Application.Messages;

public class AlbumImportedMessage
{
    public string Name { get; init; }

    public string ArtistName { get; init; }

    public string AlbumPath { get; init; }

    public AlbumImportedMessage(string name, string artistName, string path)
    {
        Name = name;
        ArtistName = artistName;
        AlbumPath = path;
    }
}
