using MiF.SimpleMessenger;
using Rok.Application.Messages;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class ImportProgressServiceTests
{
    private static (ImportProgressService service, List<LibraryRefreshMessage> received) CreateServiceAndCollector()
    {
        ImportProgressService service = new();
        List<LibraryRefreshMessage> received = new();
        Messenger.Subscribe<LibraryRefreshMessage>(received.Add);
        return (service, received);
    }

    [Fact(DisplayName = "ReportRunning should broadcast a Running state refresh message")]
    public void ReportRunning_ShouldBroadcastRunningState_RefreshMessage()
    {
        // Arrange
        (ImportProgressService service, List<LibraryRefreshMessage> received) = CreateServiceAndCollector();

        // Act
        service.ReportRunning();

        // Assert
        Assert.Contains(received, m => m.ProcessState == LibraryRefreshMessage.EState.Running);
    }

    [Fact(DisplayName = "ReportProgress should broadcast an Unchanged state with a formatted percentage message")]
    public void ReportProgress_ShouldBroadcastUnchangedState_WithFormattedPercentage()
    {
        // Arrange
        (ImportProgressService service, List<LibraryRefreshMessage> received) = CreateServiceAndCollector();

        // Act
        service.ReportProgress(73);

        // Assert
        LibraryRefreshMessage message = received.Last(m => m.ProcessState == LibraryRefreshMessage.EState.Unchanged);
        Assert.Equal("73%", message.ProcessMessage);
    }

    [Fact(DisplayName = "ReportUpdateData and ReportCleanData should broadcast their respective states")]
    public void ReportUpdateDataAndClean_ShouldBroadcastTheirRespectiveStates()
    {
        // Arrange
        (ImportProgressService service, List<LibraryRefreshMessage> received) = CreateServiceAndCollector();

        // Act
        service.ReportUpdateData();
        service.ReportCleanData();

        // Assert
        Assert.Contains(received, m => m.ProcessState == LibraryRefreshMessage.EState.UpdateData);
        Assert.Contains(received, m => m.ProcessState == LibraryRefreshMessage.EState.CleanData);
    }

    [Fact(DisplayName = "ReportStopped should broadcast Stop state with the given statistics")]
    public void ReportStopped_ShouldBroadcastStopState_WithGivenStatistics()
    {
        // Arrange
        (ImportProgressService service, List<LibraryRefreshMessage> received) = CreateServiceAndCollector();
        ImportStatisticsDto stats = new() { TracksImported = 3, TracksDeleted = 1 };

        // Act
        service.ReportStopped(stats);

        // Assert
        LibraryRefreshMessage message = received.Last(m => m.ProcessState == LibraryRefreshMessage.EState.Stop);
        Assert.Same(stats, message.Statistics);
    }
}
