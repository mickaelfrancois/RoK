using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Interfaces.Entities;

namespace Rok.ApplicationTests.Features.Artists.Requests;

public class ArtistMergeRequestHandlerTests
{
    private const string MusicFile = @"C:\music\a.mp3";

    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IPlaylistHeaderRepository> _playlists = new();
    private readonly Mock<IArtistMergeRepository> _mergeRepository = new();
    private readonly Mock<IArtistRenameRepository> _renameRepository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly ArtistMergeService _service;

    public ArtistMergeRequestHandlerTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new ArtistEntity { Id = 1, Name = "Old" }, new ArtistEntity { Id = 2, Name = "Kept" }]);
        _tracks.Setup(r => r.GetByArtistIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new TrackEntity { Id = 5, AlbumId = 2, MusicFile = MusicFile }]);
        _tracks.Setup(r => r.GetByArtistIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([]);
        _albums.Setup(r => r.GetByArtistIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => (IEnumerable<IAlbumEntity>)new List<AlbumEntity>());
        _playlists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([]);
        _renameRepository.Setup(r => r.CompleteAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteCheck(MusicFile, ETagWriteReason.None, "Old") { FileDateModified = DateTimeOffset.UnixEpoch, Size = 1 }]);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UnixEpoch, 1) { Values = new TagFieldValues("T", 1, null, "", null) { Artist = "Kept" } }]);

        _service = new ArtistMergeService(
            _artists.Object,
            _tracks.Object,
            _albums.Object,
            _playlists.Object,
            _mergeRepository.Object,
            _renameRepository.Object,
            new ArtistFileRewriter(_writer.Object, _tracks.Object, Mock.Of<IPlayerService>(), NullLogger<ArtistFileRewriter>.Instance),
            Mock.Of<IArtistPicture>(),
            Mock.Of<IImport>(),
            new LibraryWriteGate(),
            _options.Object,
            NullLogger<ArtistMergeService>.Instance);
    }

    [Fact(DisplayName = "merge_handler_returns_the_outcome_and_reports_the_progress")]
    public async Task MergeHandler_Success_ReturnsTheOutcome()
    {
        // Arrange
        MergeArtistsRequestHandler handler = new(_service);
        List<ArtistRenameProgress> reports = [];
        MergeArtistsRequest request = new(1, 2) { Progress = new ReportList(reports) };

        // Act
        Result<ArtistMergeOutcome> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(2, result.Value.ArtistId);
        Assert.Equal(1, result.Value.DeletedArtistId);
        Assert.Equal([new ArtistRenameProgress(1, 1)], reports);
        _mergeRepository.Verify(r => r.ApplyAsync(It.Is<ArtistMergeDbChange>(c => c.SourceId == 1 && c.TargetId == 2 && c.PendingFrom == "Old"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "merge_handler_returns_a_failure_result_when_the_option_is_disabled")]
    public async Task MergeHandler_OptionDisabled_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);
        MergeArtistsRequestHandler handler = new(_service);

        // Act
        Result<ArtistMergeOutcome> result = await handler.Handle(new MergeArtistsRequest(1, 2), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.merge.disabled");
    }

    [Fact(DisplayName = "merge_preview_handler_returns_the_preview")]
    public async Task PreviewHandler_Success_ReturnsThePreview()
    {
        // Arrange
        GetArtistMergePreviewRequestHandler handler = new(_service);

        // Act
        Result<ArtistMergePreview> result = await handler.Handle(new GetArtistMergePreviewRequest(1, 2), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(1, result.Value.FileCount);
        Assert.Equal("Old", result.Value.SourceName);
        Assert.Equal("Kept", result.Value.TargetName);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact(DisplayName = "merge_preview_handler_returns_not_found_for_an_unknown_artist")]
    public async Task PreviewHandler_UnknownArtist_ReturnsNotFound()
    {
        // Arrange
        GetArtistMergePreviewRequestHandler handler = new(_service);

        // Act
        Result<ArtistMergePreview> result = await handler.Handle(new GetArtistMergePreviewRequest(9, 2), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("artist.not_found");
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}