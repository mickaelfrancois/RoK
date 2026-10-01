namespace Rok.ViewModels.Albums.Interfaces;

public interface IAlbumLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    /// <summary>Raised once a library scan has imported new items, so the list reloads them in one go.</summary>
    event EventHandler? LibraryRefreshed;

    void ResetUpdateFlags();
}