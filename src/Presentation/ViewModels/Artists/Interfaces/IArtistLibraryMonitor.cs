namespace Rok.ViewModels.Artists.Interfaces;

public interface IArtistLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    /// <summary>Raised once a library scan has imported new items, so the list reloads them in one go.</summary>
    event EventHandler? LibraryRefreshed;

    void ResetUpdateFlags();
}