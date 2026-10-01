namespace Rok.ViewModels.Albums.Handlers;

public class AlbumImportedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public void Handle(AlbumImportedMessage message)
    {
        LibraryUpdated = true;
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}