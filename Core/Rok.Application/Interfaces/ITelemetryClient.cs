namespace Rok.Application.Interfaces;

public interface ITelemetryClient
{
    Task CaptureEventAsync(string eventName);

    Task CaptureScreenAsync(string screenName);

    Task CaptureExceptionAsync(Exception ex);
}