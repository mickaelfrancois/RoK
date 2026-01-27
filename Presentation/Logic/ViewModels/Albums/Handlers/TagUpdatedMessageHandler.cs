namespace Rok.Logic.ViewModels.Albums.Handlers;

public class TagUpdatedMessageHandler
{
    public bool LibraryUpdated { get; private set; }

    public event EventHandler? TagUpdated;

    public void Handle(TagUpdatedMessage message)
    {
        LibraryUpdated = true;
        TagUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}