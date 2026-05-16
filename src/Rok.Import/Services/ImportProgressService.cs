using CleanArch.DevKit.Messaging;
using Rok.Application.Dto;
using Rok.Application.Messages;

namespace Rok.Import.Services;

public class ImportProgressService(IMessenger messenger)
{
    public void ReportRunning()
    {
        messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Running
        });
    }

    public void ReportProgress(int percentage)
    {
        messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Unchanged,
            ProcessMessage = $"{percentage}%"
        });
    }

    public void ReportUpdateData()
    {
        messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.UpdateData
        });
    }

    public void ReportCleanData()
    {
        messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.CleanData
        });
    }

    public void ReportStopped(ImportStatisticsDto statistics)
    {
        messenger.Send(new LibraryRefreshMessage
        {
            ProcessState = LibraryRefreshMessage.EState.Stop,
            Statistics = statistics
        });
    }
}