namespace Rok.ViewModels.Artists.Handlers;

public class ArtistImportedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public void Handle(ArtistImportedMessage message)
    {
        LibraryUpdated = true;
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}