namespace Rok.Logic.ViewModels.Tracks.Interfaces;

public interface ITrackLibraryMonitor : IDisposable
{
    event EventHandler? LibraryChanged;

    void ResetUpdateFlags();
}