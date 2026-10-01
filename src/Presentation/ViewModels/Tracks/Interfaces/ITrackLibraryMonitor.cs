namespace Rok.ViewModels.Tracks.Interfaces;

public interface ITrackLibraryMonitor : IDisposable
{
    /// <summary>Raised once a library scan has imported new items, so the list reloads them in one go.</summary>
    event EventHandler? LibraryRefreshed;

    void ResetUpdateFlags();
}