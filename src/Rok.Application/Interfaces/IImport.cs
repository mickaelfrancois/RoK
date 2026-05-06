namespace Rok.Application.Interfaces;

public interface IImport
{
    bool UpdateInProgress { get; }

    void Start(int delayInSeconds);

    /// <summary>Cancels the import in progress and waits for it to complete.</summary>
    Task StopAsync();
}
