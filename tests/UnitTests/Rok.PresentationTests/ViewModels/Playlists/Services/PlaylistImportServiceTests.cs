using CleanArch.DevKit.Mediator.Results;
using Rok.Application.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Playlists.Requests;
using Rok.Application.Messages;
using Rok.Shared.Enums;
using Rok.ViewModels.Playlists.Services;

namespace Rok.PresentationTests.ViewModels.Playlists.Services;

public class PlaylistImportServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistFilePickerService> _picker = new();
    private readonly IMessenger _messenger = new Messenger();

    private PlaylistImportService BuildService()
        => new(_mediator.Object, _picker.Object, _messenger, NullLogger<PlaylistImportService>.Instance);

    private static Result<PlaylistImportResult> Imported(int matched, int ignored)
        => Result<PlaylistImportResult>.Ok(new PlaylistImportResult(PlaylistImportStatus.Imported, 1, "Mix", matched, ignored));

    private static Result<PlaylistImportResult> Skipped(int ignored)
        => Result<PlaylistImportResult>.Ok(new PlaylistImportResult(PlaylistImportStatus.Skipped, null, null, 0, ignored));

    private static Result<PlaylistImportResult> Failed()
        => Result<PlaylistImportResult>.Fail(new OperationError("playlist.parse_error", "Failed to parse playlist file."));

    [Fact(DisplayName = "does_not_show_toast_when_user_cancels_picker")]
    public async Task Does_not_show_toast_when_user_cancels_picker()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(Array.Empty<string>());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.Null(captured);
            _mediator.Verify(m => m.Send(It.IsAny<ImportPlaylistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "aggregates_counts_across_multiple_files")]
    public async Task Aggregates_counts_across_multiple_files()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.Send(It.IsAny<ImportPlaylistRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 5, ignored: 1))
                     .ReturnsAsync(Imported(matched: 3, ignored: 2));

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("2", captured!.Message); // 2 imported
            Assert.Contains("8", captured.Message);  // 5+3 tracks
            Assert.Contains("3", captured.Message);  // 1+2 ignored
            Assert.Equal(NotificationType.Success, captured.Type);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "toast_includes_skipped_count_when_present")]
    public async Task Toast_includes_skipped_count_when_present()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.Send(It.IsAny<ImportPlaylistRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 2, ignored: 0))
                     .ReturnsAsync(Skipped(ignored: 4));

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("vide", captured!.Message);
            Assert.Equal(NotificationType.Success, captured.Type);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "toast_includes_failed_count_when_present")]
    public async Task Toast_includes_failed_count_when_present()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8", "b.m3u8" });
            _mediator.SetupSequence(m => m.Send(It.IsAny<ImportPlaylistRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Imported(matched: 1, ignored: 0))
                     .ReturnsAsync(Failed());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Contains("échec", captured!.Message);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }

    [Fact(DisplayName = "warns_when_zero_imported_but_skipped_or_failed")]
    public async Task Warns_when_zero_imported_but_skipped_or_failed()
    {
        // Arrange
        ShowNotificationMessage? captured = null;
        void Listen(ShowNotificationMessage m) => captured = m;
        _messenger.Subscribe<ShowNotificationMessage>(Listen);
        try
        {
            _picker.Setup(p => p.PickPlaylistFilesAsync()).ReturnsAsync(new[] { "a.m3u8" });
            _mediator.Setup(m => m.Send(It.IsAny<ImportPlaylistRequest>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Failed());

            PlaylistImportService sut = BuildService();

            // Act
            await sut.RunAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(NotificationType.Warning, captured!.Type);
        }
        finally
        {
            // _messenger is instance-scoped to this test, no manual unsubscribe needed
        }
    }
}
