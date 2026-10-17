using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Requests;

public class AlbumMergeRequestHandlerTests
{
    private const string MusicFile = @"C:\music\a.flac";

    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumMergeRepository> _mergeRepository = new();
    private readonly Mock<IPendingTagWriteRepository> _pending = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly AlbumMergeService _service;

    public AlbumMergeRequestHandlerTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _albums.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new AlbumEntity { Id = 1, Name = "Src", ArtistId = 10, ArtistName = "Artist", AlbumPath = @"C:\music" });
        _albums.Setup(r => r.GetByIdAsync(2, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new AlbumEntity { Id = 2, Name = "Target", ArtistId = 10, ArtistName = "Artist", AlbumPath = @"C:\music" });
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new ArtistEntity { Id = 10, Name = "Artist" }]);
        _tracks.Setup(r => r.GetByAlbumIdAsync(1, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new TrackEntity { Id = 5, AlbumId = 1, ArtistId = 10, MusicFile = MusicFile, TrackNumber = 1 }]);
        _tracks.Setup(r => r.GetByAlbumIdAsync(2, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new TrackEntity { Id = 6, AlbumId = 2, ArtistId = 10, MusicFile = @"C:\music\b.flac", TrackNumber = 1 }]);
        _pending.Setup(r => r.CountByAlbumAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteCheck(MusicFile, ETagWriteReason.None, "Artist") { FileDateModified = DateTimeOffset.UnixEpoch, Size = 1, Current = new TagFieldValues("T", 1, null, "", null) { Album = "Src", Artist = "Artist" } }]);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UnixEpoch, 1) { Values = new TagFieldValues("T", 1, 3, "", null) { Album = "Target", Disc = 3 } }]);

        _service = new AlbumMergeService(
            _albums.Object,
            _artists.Object,
            _tracks.Object,
            _mergeRepository.Object,
            _pending.Object,
            new TrackFileTagWriter(_writer.Object, Mock.Of<IPlayerService>(), NullLogger<TrackFileTagWriter>.Instance),
            Mock.Of<IImport>(),
            new LibraryWriteGate(),
            _options.Object,
            NullLogger<AlbumMergeService>.Instance);
    }

    [Fact(DisplayName = "merge_handler_returns_the_outcome_and_reports_the_progress")]
    public async Task MergeHandler_Success_ReturnsTheOutcome()
    {
        // Arrange
        MergeAlbumsRequestHandler handler = new(_service);
        List<ArtistRenameProgress> reports = [];
        MergeAlbumsRequest request = new(1, 2, true) { Progress = new ReportList(reports) };

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(2, result.Value.AlbumId);
        Assert.Equal([1L], result.Value.DeletedAlbumIds);
        Assert.Equal([new ArtistRenameProgress(1, 1)], reports);
        _mergeRepository.Verify(r => r.ApplyAsync(It.Is<AlbumMergeDbChange>(c => c.SourceId == 1 && c.TargetId == 2 && c.DiscOffset == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "merge_handler_refuses_colliding_numbers_when_the_shift_is_not_asked")]
    public async Task MergeHandler_ConflictWithoutShift_ReturnsFailure()
    {
        // Arrange
        MergeAlbumsRequestHandler handler = new(_service);

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(new MergeAlbumsRequest(1, 2, false), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.conflicts");
    }

    [Fact(DisplayName = "merge_handler_returns_a_failure_result_when_the_option_is_disabled")]
    public async Task MergeHandler_OptionDisabled_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);
        MergeAlbumsRequestHandler handler = new(_service);

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(new MergeAlbumsRequest(1, 2, false), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.disabled");
    }

    [Fact(DisplayName = "merge_preview_handler_returns_the_preview")]
    public async Task PreviewHandler_Success_ReturnsThePreview()
    {
        // Arrange
        GetAlbumMergePreviewRequestHandler handler = new(_service);

        // Act
        Result<AlbumMergePreview> result = await handler.Handle(new GetAlbumMergePreviewRequest(1, 2), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(1, result.Value.FileCount);
        Assert.Equal("Src", result.Value.SourceName);
        Assert.Equal("Target", result.Value.TargetName);
        Assert.Single(result.Value.DiscConflicts);
        Assert.Equal(1, result.Value.SuggestedDiscOffset);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact(DisplayName = "merge_preview_handler_returns_not_found_for_an_unknown_album")]
    public async Task PreviewHandler_UnknownAlbum_ReturnsNotFound()
    {
        // Arrange
        GetAlbumMergePreviewRequestHandler handler = new(_service);

        // Act
        Result<AlbumMergePreview> result = await handler.Handle(new GetAlbumMergePreviewRequest(99, 2), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.not_found");
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}