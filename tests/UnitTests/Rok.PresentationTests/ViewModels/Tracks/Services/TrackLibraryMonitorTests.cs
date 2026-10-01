using CleanArch.DevKit.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Dto;
using Rok.Application.Messages;
using Rok.ViewModels.Albums.Handlers;
using Rok.ViewModels.Tracks.Handlers;
using Rok.ViewModels.Tracks.Services;

namespace Rok.PresentationTests.ViewModels.Tracks.Services;

public class TrackLibraryMonitorTests
{
    private readonly Messenger _messenger = new();

    private TrackLibraryMonitor CreateMonitor() =>
        new(_messenger, new LibraryRefreshMessageHandler(NullLogger<LibraryRefreshMessageHandler>.Instance), new TrackImportedMessageHandler());

    [Fact(DisplayName = "album_imported_during_a_scan_does_not_refresh_the_list")]
    public void AlbumImported_DoesNotRefresh()
    {
        // Arrange
        using TrackLibraryMonitor sut = CreateMonitor();
        int refreshCount = 0;
        sut.LibraryRefreshed += (_, _) => refreshCount++;

        // Act
        for (int i = 0; i < 50; i++)
            _messenger.Send(new AlbumImportedMessage($"Album {i}", "Artist", $"Music/{i}"));

        // Assert
        Assert.Equal(0, refreshCount);
    }

    [Fact(DisplayName = "scan_stop_with_imported_items_refreshes_the_list_once")]
    public void ScanStopWithImports_RefreshesOnce()
    {
        // Arrange
        using TrackLibraryMonitor sut = CreateMonitor();
        int refreshCount = 0;
        sut.LibraryRefreshed += (_, _) => refreshCount++;

        // Act
        _messenger.Send(new LibraryRefreshMessage { ProcessState = LibraryRefreshMessage.EState.Running });
        _messenger.Send(new LibraryRefreshMessage { ProcessState = LibraryRefreshMessage.EState.Unchanged, ProcessMessage = "50%" });
        _messenger.Send(new LibraryRefreshMessage { ProcessState = LibraryRefreshMessage.EState.Stop, Statistics = new ImportStatisticsDto { AlbumsImported = 120 } });

        // Assert
        Assert.Equal(1, refreshCount);
    }

    [Fact(DisplayName = "scan_stop_without_change_does_not_refresh_the_list")]
    public void ScanStopWithoutChange_DoesNotRefresh()
    {
        // Arrange
        using TrackLibraryMonitor sut = CreateMonitor();
        int refreshCount = 0;
        sut.LibraryRefreshed += (_, _) => refreshCount++;

        // Act
        _messenger.Send(new LibraryRefreshMessage { ProcessState = LibraryRefreshMessage.EState.Stop, Statistics = new ImportStatisticsDto() });

        // Assert
        Assert.Equal(0, refreshCount);
    }
}