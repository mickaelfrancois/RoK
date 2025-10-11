namespace Rok.Application.Messages;

public class LibraryRefreshMessage
{
    public enum EState { Running, Stop, CleanData, UpdateData, Cancelled, Unchanged }

    public EState ProcessState { get; set; }

    public string ProcessMessage { get; set; } = string.Empty;

    public ImportStatisticsDto Statistics { get; set; } = new();
}