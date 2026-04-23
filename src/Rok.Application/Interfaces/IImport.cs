namespace Rok.Application.Interfaces;

public interface IImport
{
    bool UpdateInProgress { get; }

    void StartAsync(int delayInSeconds);
}
