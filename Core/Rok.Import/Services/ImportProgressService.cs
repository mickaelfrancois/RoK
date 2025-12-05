using MiF.SimpleMessenger;
using Rok.Application.Dto;
using Rok.Application.Messages;

namespace Rok.Import.Services;

public class ImportProgressService
{
    public void ReportRunning()
    {
        Messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Running
        });
    }

    public void ReportProgress(int percentage)
    {
        Messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Unchanged,
            ProcessMessage = $"{percentage}%"
        });
    }

    public void ReportUpdateData()
    {
        Messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.UpdateData
        });
    }

    public void ReportCleanData()
    {
        Messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.CleanData
        });
    }

    public void ReportStopped(ImportStatisticsDto statistics)
    {
        Messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Stop,
            Statistics = statistics
        });
    }
}