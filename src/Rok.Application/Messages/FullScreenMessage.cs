namespace Rok.Application.Messages;

public class FullScreenMessage(bool isFullScreen)
{
    public bool IsFullScreen { get; set; } = isFullScreen;
}
