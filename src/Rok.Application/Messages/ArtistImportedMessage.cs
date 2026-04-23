namespace Rok.Application.Messages;

public class ArtistImportedMessage
{
    public string Name { get; init; }

    public ArtistImportedMessage(string name)
    {
        Name = name;
    }
}
