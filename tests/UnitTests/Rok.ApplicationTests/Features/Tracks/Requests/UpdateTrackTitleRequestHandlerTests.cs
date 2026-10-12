using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Tracks.Requests;

public class UpdateTrackTitleRequestHandlerTests
{
    private readonly Mock<ITrackRepository> _repository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IAppOptions> _options = new();

    private UpdateTrackTitleRequestHandler CreateHandler()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new TrackEntity { Id = 1, MusicFile = @"C:\music\song.mp3" });
        _repository.Setup(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(true);

        TrackTagEditService service = new(_repository.Object, _writer.Object, Mock.Of<IPlayerService>(), Mock.Of<IImport>(), new LibraryWriteGate(), _options.Object, NullLogger<TrackTagEditService>.Instance);

        return new UpdateTrackTitleRequestHandler(service);
    }

    [Fact(DisplayName = "when_the_write_succeeds_the_trimmed_title_is_returned")]
    public async Task Handle_Success_ReturnsTrimmedTitle()
    {
        // Arrange
        UpdateTrackTitleRequestHandler handler = CreateHandler();
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(@"C:\music\song.mp3", ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UtcNow, 10)]);

        // Act
        Result<string> result = await handler.Handle(new UpdateTrackTitleRequest(1, "  New title  "), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        _repository.Verify(r => r.UpdateTitleAndFileInfoAsync(1, "New title", It.IsAny<DateTime>(), 10, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Theory(DisplayName = "when_the_writer_refuses_the_error_code_names_the_reason")]
    [InlineData(ETagWriteReason.Locked, "track.tag_write.locked")]
    [InlineData(ETagWriteReason.ReadOnly, "track.tag_write.read_only")]
    [InlineData(ETagWriteReason.UnsupportedFormat, "track.tag_write.unsupported_format")]
    [InlineData(ETagWriteReason.CloudPlaceholder, "track.tag_write.cloud_placeholder")]
    [InlineData(ETagWriteReason.UnsupportedTagVersion, "track.tag_write.unsupported_tag_version")]
    [InlineData(ETagWriteReason.InsufficientDiskSpace, "track.tag_write.insufficient_disk_space")]
    [InlineData(ETagWriteReason.NotFound, "track.tag_write.not_found")]
    [InlineData(ETagWriteReason.VerificationFailed, "track.tag_write.verification_failed")]
    public async Task Handle_WriterRefuses_ReturnsReasonCode(ETagWriteReason reason, string code)
    {
        // Arrange
        UpdateTrackTitleRequestHandler handler = CreateHandler();
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(@"C:\music\song.mp3", ETagWriteOutcome.Skipped, reason, null, null)]);

        // Act
        Result<string> result = await handler.Handle(new UpdateTrackTitleRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode(code);
    }

    [Fact(DisplayName = "when_the_option_is_disabled_the_error_code_is_disabled")]
    public async Task Handle_OptionDisabled_ReturnsDisabledCode()
    {
        // Arrange
        UpdateTrackTitleRequestHandler handler = CreateHandler();
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<string> result = await handler.Handle(new UpdateTrackTitleRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.tag_write.disabled");
    }

    [Fact(DisplayName = "when_the_track_is_playing_the_error_code_is_playing")]
    public async Task Handle_TrackPlaying_ReturnsPlayingCode()
    {
        // Arrange
        UpdateTrackTitleRequestHandler handler = CreateHandler();
        Mock<IPlayerService> player = new();
        player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 1 });
        TrackTagEditService service = new(_repository.Object, _writer.Object, player.Object, Mock.Of<IImport>(), new LibraryWriteGate(), _options.Object, NullLogger<TrackTagEditService>.Instance);
        handler = new UpdateTrackTitleRequestHandler(service);

        // Act
        Result<string> result = await handler.Handle(new UpdateTrackTitleRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.tag_write.playing");
    }
}