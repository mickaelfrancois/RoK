namespace Rok.ViewModels.Artists.Interfaces;

public interface IArtistLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    void ResetUpdateFlags();
}