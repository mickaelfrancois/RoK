namespace Rok.ViewModels.Tracks.Handlers;

public class TrackImportedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public event EventHandler? TrackImported;

    public void Handle(AlbumImportedMessage message)
    {
        LibraryUpdated = true;
        TrackImported?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}