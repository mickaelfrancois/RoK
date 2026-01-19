namespace Rok.Logic.ViewModels.Albums.Interfaces;

public interface IAlbumLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    void ResetUpdateFlags();
}