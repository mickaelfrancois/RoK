using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;

namespace Rok.ApplicationTests.Features.Artists.Requests;

public class ArtistRenameRequestHandlerTests
{
    private const string MusicFile = @"C:\music\a.mp3";

    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IPlaylistHeaderRepository> _playlists = new();
    private readonly Mock<IArtistRenameRepository> _renameRepository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IArtistPicture> _picture = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly ArtistRenameService _service;

    public ArtistRenameRequestHandlerTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new ArtistEntity { Id = 1, Name = "Old" }]);
        _tracks.Setup(r => r.GetByArtistIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new TrackEntity { Id = 5, AlbumId = 2, MusicFile = MusicFile }]);
        _playlists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([]);
        _renameRepository.Setup(r => r.CompleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteCheck(MusicFile, ETagWriteReason.None, "Old") { FileDateModified = DateTimeOffset.UnixEpoch, Size = 1 }]);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UnixEpoch, 1) { Values = new TagFieldValues("T", 1, null, "", null) { Artist = "New" } }]);

        _service = new ArtistRenameService(
            _artists.Object,
            _tracks.Object,
            _playlists.Object,
            _renameRepository.Object,
            _writer.Object,
            _picture.Object,
            Mock.Of<IPlayerService>(),
            Mock.Of<IImport>(),
            new LibraryWriteGate(),
            _options.Object,
            NullLogger<ArtistRenameService>.Instance);
    }

    [Fact(DisplayName = "rename_handler_returns_the_outcome_and_reports_the_progress")]
    public async Task RenameHandler_Success_ReturnsTheOutcome()
    {
        // Arrange
        RenameArtistRequestHandler handler = new(_service);
        List<ArtistRenameProgress> reports = [];
        RenameArtistRequest request = new(1, "New") { Progress = new ReportList(reports) };

        // Act
        Result<ArtistRenameOutcome> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal([new ArtistRenameProgress(1, 1)], reports);
    }

    [Fact(DisplayName = "rename_handler_returns_a_failure_result_when_the_option_is_disabled")]
    public async Task RenameHandler_OptionDisabled_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);
        RenameArtistRequestHandler handler = new(_service);

        // Act
        Result<ArtistRenameOutcome> result = await handler.Handle(new RenameArtistRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.rename.disabled");
    }

    [Fact(DisplayName = "preview_handler_returns_the_preview")]
    public async Task PreviewHandler_Success_ReturnsThePreview()
    {
        // Arrange
        GetArtistRenamePreviewRequestHandler handler = new(_service);

        // Act
        Result<ArtistRenamePreview> result = await handler.Handle(new GetArtistRenamePreviewRequest(1, "New"), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(1, result.Value.FileCount);
        Assert.Equal("Old", result.Value.OldName);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact(DisplayName = "preview_handler_returns_not_found_for_an_unknown_artist")]
    public async Task PreviewHandler_UnknownArtist_ReturnsNotFound()
    {
        // Arrange
        GetArtistRenamePreviewRequestHandler handler = new(_service);

        // Act
        Result<ArtistRenamePreview> result = await handler.Handle(new GetArtistRenamePreviewRequest(9, "New"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("artist.not_found");
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}