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

public class UpdateTrackTagsRequestHandlerTests
{
    private const string MusicFile = @"C:\music\song.mp3";

    private readonly Mock<ITrackRepository> _repository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IAppOptions> _options = new();

    private UpdateTrackTagsRequestHandler CreateHandler(IPlayerService? player = null)
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new TrackEntity { Id = 1, MusicFile = MusicFile });
        _repository.Setup(r => r.UpdateTagFieldsAndFileInfoAsync(It.IsAny<long>(), It.IsAny<TagFieldValues>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(true);

        TrackTagEditService service = new(_repository.Object, _writer.Object, player ?? Mock.Of<IPlayerService>(), Mock.Of<IImport>(), new LibraryWriteGate(), _options.Object, NullLogger<TrackTagEditService>.Instance);

        return new UpdateTrackTagsRequestHandler(service);
    }

    private void SetupWrite(TagWriteResult result)
    {
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([result]);
    }

    [Fact(DisplayName = "handler_returns_the_saved_values")]
    public async Task Handle_Success_ReturnsSavedValues()
    {
        // Arrange
        UpdateTrackTagsRequestHandler handler = CreateHandler();
        DateTimeOffset fileDate = new(2025, 3, 4, 5, 6, 7, TimeSpan.Zero);
        SetupWrite(new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, fileDate, 10) { Values = new TagFieldValues("New title", 5, 2, "Bach", 120) });

        // Act
        Result<TrackEditSaved> result = await handler.Handle(new UpdateTrackTagsRequest(1, "New title", 5, 2, "Bach", 120), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(new TrackEditSaved("New title", 5, 2, "Bach", 120, fileDate.DateTime, 10), result.Value);
    }

    [Fact(DisplayName = "when_the_write_succeeds_the_trimmed_title_and_composers_are_sent_to_the_writer")]
    public async Task Handle_Success_TrimsTitleAndComposers()
    {
        // Arrange
        UpdateTrackTagsRequestHandler handler = CreateHandler();
        SetupWrite(new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UtcNow, 10) { Values = new TagFieldValues("New title", 1, null, "Bach", null) });

        // Act
        await handler.Handle(new UpdateTrackTagsRequest(1, "  New title  ", composers: "  Bach  "), CancellationToken.None);

        // Assert
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(l => l[0].Changes.Title == "New title" && l[0].Changes.Composers == "Bach"), It.IsAny<CancellationToken>()), Times.Once);
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
        UpdateTrackTagsRequestHandler handler = CreateHandler();
        SetupWrite(new TagWriteResult(MusicFile, ETagWriteOutcome.Skipped, reason, null, null));

        // Act
        Result<TrackEditSaved> result = await handler.Handle(new UpdateTrackTagsRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode(code);
    }

    [Fact(DisplayName = "when_the_option_is_disabled_the_error_code_is_disabled")]
    public async Task Handle_OptionDisabled_ReturnsDisabledCode()
    {
        // Arrange
        UpdateTrackTagsRequestHandler handler = CreateHandler();
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<TrackEditSaved> result = await handler.Handle(new UpdateTrackTagsRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.tag_write.disabled");
    }

    [Fact(DisplayName = "when_the_track_is_playing_the_error_code_is_playing")]
    public async Task Handle_TrackPlaying_ReturnsPlayingCode()
    {
        // Arrange
        Mock<IPlayerService> player = new();
        player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 1 });
        UpdateTrackTagsRequestHandler handler = CreateHandler(player.Object);

        // Act
        Result<TrackEditSaved> result = await handler.Handle(new UpdateTrackTagsRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<OperationError>().And.HaveErrorWithCode("track.tag_write.playing");
    }
}