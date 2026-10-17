using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Requests;

public class AlbumEditRequestHandlerTests
{
    private const string MusicFile = @"C:\music\a.flac";

    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<IGenreRepository> _genres = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumEditRepository> _editRepository = new();
    private readonly Mock<IPendingTagWriteRepository> _pending = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly AlbumEditService _service;

    public AlbumEditRequestHandlerTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _albums.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new AlbumEntity { Id = 1, Name = "Old", ArtistId = 10, ArtistName = "Artist", GenreId = 20, GenreName = "Rock" });
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new ArtistEntity { Id = 10, Name = "Artist" }]);
        _genres.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync([new GenreEntity { Id = 20, Name = "Rock" }]);
        _tracks.Setup(r => r.GetByAlbumIdAsync(1, It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync([new TrackEntity { Id = 5, AlbumId = 1, ArtistId = 10, GenreId = 20, MusicFile = MusicFile, ArtistName = "Artist", GenreName = "Rock" }]);
        _pending.Setup(r => r.CountByAlbumAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _editRepository.Setup(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AlbumEditApplied(10, 20));
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteCheck(MusicFile, ETagWriteReason.None, "Artist") { FileDateModified = DateTimeOffset.UnixEpoch, Size = 1, Current = new TagFieldValues("T", 1, null, "", null) { Album = "Old", Artist = "Artist", Genre = "Rock" } }]);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UnixEpoch, 1) { Values = new TagFieldValues("T", 1, null, "", null) { Album = "New" } }]);

        _service = new AlbumEditService(
            _albums.Object,
            _artists.Object,
            _genres.Object,
            _tracks.Object,
            _editRepository.Object,
            _pending.Object,
            new TrackFileTagWriter(_writer.Object, Mock.Of<IPlayerService>(), NullLogger<TrackFileTagWriter>.Instance),
            Mock.Of<IImport>(),
            new LibraryWriteGate(),
            _options.Object,
            NullLogger<AlbumEditService>.Instance);
    }

    [Fact(DisplayName = "edit_handler_returns_the_outcome_and_reports_the_progress")]
    public async Task EditHandler_Success_ReturnsTheOutcome()
    {
        // Arrange
        EditAlbumTagsRequestHandler handler = new(_service);
        List<ArtistRenameProgress> reports = [];
        EditAlbumTagsRequest request = new(1, "New", "Artist", null, null) { Progress = new ReportList(reports) };

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal([new ArtistRenameProgress(1, 1)], reports);
        _editRepository.Verify(r => r.ApplyAsync(It.Is<AlbumEditDbChange>(c => c.Name == "New"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "edit_handler_returns_a_failure_result_when_the_option_is_disabled")]
    public async Task EditHandler_OptionDisabled_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);
        EditAlbumTagsRequestHandler handler = new(_service);

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(new EditAlbumTagsRequest(1, "New", "Artist", null, null), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.disabled");
    }

    [Fact(DisplayName = "edit_handler_refuses_a_genre_change_when_the_import_gives_tracks_the_genre_of_their_artist_and_writes_nothing")]
    public async Task EditHandler_GenreChangeWithArtistGenreOption_ReturnsFailureAndWritesNothing()
    {
        // Arrange
        _options.SetupGet(o => o.ImportTrackWithArtistGenre).Returns(true);
        EditAlbumTagsRequestHandler handler = new(_service);

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(new EditAlbumTagsRequest(1, "Old", "Artist", null, "Jazz"), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.genre_from_artist");
        _editRepository.Verify(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "preview_handler_returns_the_preview")]
    public async Task PreviewHandler_Success_ReturnsThePreview()
    {
        // Arrange
        GetAlbumEditPreviewRequestHandler handler = new(_service);

        // Act
        Result<AlbumEditPreview> result = await handler.Handle(new GetAlbumEditPreviewRequest(1, "New", "Artist", 2001, "Rock"), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(1, result.Value.FileCount);
        Assert.Equal("Old", result.Value.OldName);
        Assert.Equal("New", result.Value.NewName);
        Assert.Equal(2001, result.Value.NewYear);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact(DisplayName = "preview_handler_returns_not_found_for_an_unknown_album")]
    public async Task PreviewHandler_UnknownAlbum_ReturnsNotFound()
    {
        // Arrange
        GetAlbumEditPreviewRequestHandler handler = new(_service);

        // Act
        Result<AlbumEditPreview> result = await handler.Handle(new GetAlbumEditPreviewRequest(99, "New", "Artist", null, null), CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.not_found");
    }

    [Fact(DisplayName = "resume_handler_rewrites_the_pending_files")]
    public async Task ResumeHandler_PendingRow_WritesAndCompletes()
    {
        // Arrange
        _pending.Setup(r => r.GetByAlbumAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([new PendingTagWrite(5, ETagWriteFields.Album)]);
        ResumeAlbumTagWriteRequestHandler handler = new(_service);

        // Act
        Result<AlbumTagWriteOutcome> result = await handler.Handle(new ResumeAlbumTagWriteRequest(1), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        _pending.Verify(r => r.CompleteAsync(5, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}