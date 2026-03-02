namespace Rok.Application.Interfaces;

public interface ICallDetectionService
{
    event EventHandler<bool> CallStateChanged;

    void Start();

    void Stop();
}