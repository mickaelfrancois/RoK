using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Compilation;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Enums;
using Rok.Domain.Interfaces.Entities;

namespace Rok.ApplicationTests.Features.Albums.Compilation;

public class AlbumCompilationServiceTests
{
    private const long AlbumId = 1;
    private const long ArtistId = 10;
    private const long GenreId = 20;

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumCompilationRepository> _repository = new();
    private readonly Mock<IPendingTagWriteRepository> _pending = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly List<string> _calls = [];
    private readonly SetAlbumCompilationRequestHandler _sut;

    public AlbumCompilationServiceTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _albums.Setup(r => r.GetByIdAsync(AlbumId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Album);
        _albums.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => AllAlbums);
        _albums.Setup(r => r.GetByArtistIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync((long artistId, RepositoryConnectionKind _) => (IEnumerable<IAlbumEntity>)AllAlbums.Where(a => a.ArtistId == artistId).ToList());
        _tracks.Setup(r => r.GetByAlbumIdAsync(AlbumId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => TrackList);
        _pending.Setup(r => r.CountByAlbumAsync(AlbumId, It.IsAny<CancellationToken>())).ReturnsAsync(() => PendingCount);
        _pending.Setup(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<long, DateTime, long, CancellationToken>((id, _, _, _) => _calls.Add($"complete:{id}"))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.ApplyAsync(It.IsAny<AlbumCompilationDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<AlbumCompilationDbChange, CancellationToken>((change, _) =>
            {
                _calls.Add("apply");
                LastChange = change;
                GateWasHeldDuringApply = !_gate.TryEnter(out IDisposable? lease);
                lease?.Dispose();
            })
            .Returns(Task.CompletedTask);
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) =>
                (IReadOnlyList<TagWriteCheck>)paths.Select(p => new TagWriteCheck(p, ETagWriteReason.None, "Artist") { FileDateModified = FileDate, Size = 99, Current = Values(false) }).ToList());
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
            {
                _calls.Add($"write:{requests[0].FilePath}");

                return (IReadOnlyList<TagWriteResult>)[new TagWriteResult(requests[0].FilePath, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = Values(requests[0].Changes.IsCompilation == true) }];
            });

        TrackFileTagWriter fileWriter = new(_writer.Object, _player.Object, NullLogger<TrackFileTagWriter>.Instance);
        AlbumCompilationService service = new(_albums.Object, _tracks.Object, _repository.Object, _pending.Object, fileWriter, _import.Object, _gate, _options.Object, NullLogger<AlbumCompilationService>.Instance);

        _sut = new SetAlbumCompilationRequestHandler(service);
    }

    private AlbumCompilationDbChange? LastChange { get; set; }

    private bool GateWasHeldDuringApply { get; set; }

    private int PendingCount { get; set; }

    private AlbumEntity Album { get; set; } = new() { Id = AlbumId, Name = "Hits", ArtistId = ArtistId, GenreId = GenreId, TrackCount = 2 };

    private List<AlbumEntity> AllAlbums { get; set; } = [];

    private List<TrackEntity> TrackList { get; set; } =
    [
        new TrackEntity { Id = 100, AlbumId = AlbumId, ArtistId = ArtistId, MusicFile = @"C:\music\a.flac" },
        new TrackEntity { Id = 101, AlbumId = AlbumId, ArtistId = ArtistId, MusicFile = @"C:\music\b.flac" }
    ];

    private static TagFieldValues Values(bool isCompilation)
    {
        return new TagFieldValues("T", 1, null, "", null) { Album = "Hits", Artist = "Artist", IsCompilation = isCompilation };
    }

    private Task<Result<AlbumTagWriteOutcome>> SetAsync(bool isCompilation)
    {
        return _sut.Handle(new SetAlbumCompilationRequest(AlbumId, isCompilation), CancellationToken.None);
    }

    private void VerifyNothingChanged()
    {
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.ApplyAsync(It.IsAny<AlbumCompilationDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "set_compilation_with_tags_on_stores_pending_rows_then_writes_and_completes_each_file")]
    public async Task SetAsync_Check_WithTagsOn_AppliesDatabaseThenWritesAndCompletesEachFile()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(["apply", @"write:C:\music\a.flac", "complete:100", @"write:C:\music\b.flac", "complete:101"], _calls);
        Assert.NotNull(LastChange);
        Assert.True(LastChange.IsCompilation);
        Assert.Equal(ArtistId, LastChange.ArtistId);
        Assert.Equal([new PendingTagWrite(100, ETagWriteFields.Compilation), new PendingTagWrite(101, ETagWriteFields.Compilation)], LastChange.PendingWrites);
        Assert.True(GateWasHeldDuringApply);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.IsCompilation == true), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "set_compilation_uncheck_writes_the_flag_removal")]
    public async Task SetAsync_Uncheck_WritesFlagRemoval()
    {
        // Arrange
        Album.IsCompilation = true;

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(false);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.False(LastChange!.IsCompilation);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.IsCompilation == false), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "set_compilation_with_tags_off_updates_the_database_only")]
    public async Task SetAsync_WithTagsOff_AppliesDatabaseOnlyWithoutPendingRows()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(["apply"], _calls);
        Assert.Empty(LastChange!.PendingWrites);
        _writer.Verify(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "set_compilation_is_refused_while_a_write_is_pending")]
    public async Task SetAsync_PendingRows_IsRefused()
    {
        // Arrange
        PendingCount = 1;

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.pending");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_is_refused_when_another_compilation_has_the_same_name")]
    public async Task SetAsync_Check_CollisionWithACompilation_IsRefused()
    {
        // Arrange
        AllAlbums = [new AlbumEntity { Id = 2, Name = "HITS", ArtistId = 11, IsCompilation = true }];

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("album.compilation.collision");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_uncheck_is_refused_when_the_artist_already_has_that_album")]
    public async Task SetAsync_Uncheck_CollisionWithAnAlbumOfTheArtist_IsRefused()
    {
        // Arrange
        Album.IsCompilation = true;
        AllAlbums = [new AlbumEntity { Id = 2, Name = "Hits", ArtistId = ArtistId }];

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(false);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.compilation.collision");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_is_blocked_while_an_import_runs")]
    public async Task SetAsync_ImportInProgress_ReturnsBlocked()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_is_blocked_while_the_library_write_gate_is_busy")]
    public async Task SetAsync_GateBusy_ReturnsBlocked()
    {
        // Arrange
        using IDisposable? held = _gate.TryEnter(out IDisposable? lease) ? lease : null;

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_writes_nothing_when_a_file_is_a_blocker")]
    public async Task SetAsync_UnreadableFile_ReturnsBlockedWithoutChange()
    {
        // Arrange
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) =>
                (IReadOnlyList<TagWriteCheck>)paths.Select(p => new TagWriteCheck(p, p.EndsWith("b.flac") ? ETagWriteReason.NotFound : ETagWriteReason.None, "Artist") { Current = Values(false) }).ToList());

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_keeps_the_pending_row_of_a_file_that_fails")]
    public async Task SetAsync_OneFileFails_ReturnsPartialAndKeepsItsPendingRow()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
                (IReadOnlyList<TagWriteResult>)[requests[0].FilePath.EndsWith("b.flac")
                    ? new TagWriteResult(requests[0].FilePath, ETagWriteOutcome.Failed, ETagWriteReason.IoError, null, null)
                    : new TagWriteResult(requests[0].FilePath, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = Values(true) }]);

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Single(result.Value.Failures);
        _pending.Verify(r => r.CompleteAsync(101, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _pending.Verify(r => r.CompleteAsync(100, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "set_compilation_writes_no_file_when_the_database_update_fails")]
    public async Task SetAsync_DatabaseFails_ReturnsFailedWithoutWriting()
    {
        // Arrange
        _repository.Setup(r => r.ApplyAsync(It.IsAny<AlbumCompilationDbChange>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        Assert.Equal(EArtistRenameState.Failed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "set_compilation_with_the_current_status_is_refused")]
    public async Task SetAsync_SameStatus_IsRefused()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(false);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.compilation.unchanged");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "set_compilation_of_an_unknown_album_is_not_found")]
    public async Task SetAsync_UnknownAlbum_ReturnsNotFound()
    {
        // Arrange
        Album = null!;

        // Act
        Result<AlbumTagWriteOutcome> result = await SetAsync(true);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>();
        VerifyNothingChanged();
    }
}