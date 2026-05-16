using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Playlists.Requests;
using Rok.ViewModels.Playlist.Services;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistExportServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IPlaylistExportPrompts> _prompts = new();

    private PlaylistExportService BuildService()
        => new(_mediator.Object, _prompts.Object, NullLogger<PlaylistExportService>.Instance);

    [Fact(DisplayName = "shows_warning_dialog_for_smart_playlist_before_picker")]
    public async Task Shows_warning_dialog_for_smart_playlist_before_picker()
    {
        // Arrange
        _prompts.Setup(p => p.ConfirmSmartPlaylistExportAsync()).ReturnsAsync(true);
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\out.m3u8");
        _mediator.Setup(m => m.Send(It.IsAny<ExportPlaylistRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Smart", Type = 0 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.ConfirmSmartPlaylistExportAsync(), Times.Once);
        _prompts.Verify(p => p.PickSavePathAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "does_not_show_warning_for_classic_playlist")]
    public async Task Does_not_show_warning_for_classic_playlist()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\out.m3u8");
        _mediator.Setup(m => m.Send(It.IsAny<ExportPlaylistRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Classic", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.ConfirmSmartPlaylistExportAsync(), Times.Never);
    }

    [Fact(DisplayName = "does_not_call_handler_when_dialog_cancelled")]
    public async Task Does_not_call_handler_when_dialog_cancelled()
    {
        // Arrange
        _prompts.Setup(p => p.ConfirmSmartPlaylistExportAsync()).ReturnsAsync(false);

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Smart", Type = 0 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _prompts.Verify(p => p.PickSavePathAsync(It.IsAny<string>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<ExportPlaylistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "does_not_call_handler_when_picker_cancelled")]
    public async Task Does_not_call_handler_when_picker_cancelled()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        PlaylistHeaderDto playlist = new() { Id = 1, Name = "Mix", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Send(It.IsAny<ExportPlaylistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "passes_chosen_path_to_export_command")]
    public async Task Passes_chosen_path_to_export_command()
    {
        // Arrange
        _prompts.Setup(p => p.PickSavePathAsync(It.IsAny<string>())).ReturnsAsync("X:\\final.m3u8");
        _mediator.Setup(m => m.Send(It.IsAny<ExportPlaylistRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        PlaylistHeaderDto playlist = new() { Id = 42, Name = "Mix", Type = 1 };
        PlaylistExportService sut = BuildService();

        // Act
        await sut.RunAsync(playlist, CancellationToken.None);

        // Assert
        _mediator.Verify(m => m.Send(
            It.Is<ExportPlaylistRequest>(c => c.PlaylistId == 42 && c.FilePath == "X:\\final.m3u8"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
