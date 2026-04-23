using Rok.Shared.Enums;

namespace Rok.Application.Messages;

public class ShowNotificationMessage
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.Informational;
}
