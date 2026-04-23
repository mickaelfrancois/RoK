namespace Rok.ViewModels.Albums.Handlers;

public class AlbumImportedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public event EventHandler? AlbumImported;

    public void Handle(AlbumImportedMessage message)
    {
        LibraryUpdated = true;
        AlbumImported?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}