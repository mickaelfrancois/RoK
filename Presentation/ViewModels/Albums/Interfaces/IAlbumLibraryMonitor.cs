namespace Rok.ViewModels.Albums.Interfaces;

public interface IAlbumLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    void ResetUpdateFlags();
}