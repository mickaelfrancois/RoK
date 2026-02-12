namespace Rok.ViewModels.Albums.Handlers;

public class LibraryRefreshMessageHandler(ILogger<LibraryRefreshMessageHandler> logger)
{
    public bool LibraryUpdated { get; private set; }

    public event EventHandler? LibraryChanged;

    public void Handle(LibraryRefreshMessage message)
    {
        if (message.Statistics.HasAnyImport)
        {
            logger.LogInformation("Library updated with {Count} new items.", message.Statistics.TotalCount);
            LibraryUpdated = true;
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetLibraryUpdatedFlag()
    {
        LibraryUpdated = false;
    }
}