namespace Rok.ViewModels.Tracks.Handlers;

public class TrackImportedMessageHandler
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