namespace Rok.ViewModels.Artists.Handlers;

public class ArtistImportedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public event EventHandler? ArtistImported;

    public void Handle(ArtistImportedMessage message)
    {
        LibraryUpdated = true;
        ArtistImported?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}