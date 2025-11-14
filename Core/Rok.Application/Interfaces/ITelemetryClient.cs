namespace Rok.Application.Interfaces;

public interface ITelemetryClient
{
    Task CaptureEventAsync(string eventName, Dictionary<string, object>? properties = null);

    Task CaptureScreenAsync(string screenName);

    Task CaptureExceptionAsync(Exception ex);
}