using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Interfaces.Entities;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Edit;

public class AlbumEditServiceTests
{
    private const long AlbumId = 1;
    private const long ArtistId = 10;
    private const long GenreId = 20;

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<IGenreRepository> _genres = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumEditRepository> _editRepository = new();
    private readonly Mock<IPendingTagWriteRepository> _pending = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly List<string> _calls = [];
    private readonly AlbumEditService _sut;

    public AlbumEditServiceTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _albums.Setup(r => r.GetByIdAsync(AlbumId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Album);
        _albums.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => AllAlbums);
        _albums.Setup(r => r.GetByArtistIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync((long artistId, RepositoryConnectionKind _) => (IEnumerable<IAlbumEntity>)AllAlbums.Where(a => a.ArtistId == artistId).ToList());
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Artists);
        _genres.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Genres);
        _tracks.Setup(r => r.GetByAlbumIdAsync(AlbumId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => TrackList);
        _tracks.Setup(r => r.GetByArtistIdAsync(ArtistId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => ArtistTracks);
        _pending.Setup(r => r.CountByAlbumAsync(AlbumId, It.IsAny<CancellationToken>())).ReturnsAsync(() => PendingRows.Count);
        _pending.Setup(r => r.GetByAlbumAsync(AlbumId, It.IsAny<CancellationToken>())).ReturnsAsync(() => PendingRows);
        _pending.Setup(r => r.DeleteOrphansAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => _calls.Add("orphans"))
            .ReturnsAsync(0);
        _pending.Setup(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<long, DateTime, long, CancellationToken>((id, _, _, _) => _calls.Add($"complete:{id}"))
            .Returns(Task.CompletedTask);
        _editRepository.Setup(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<AlbumEditDbChange, CancellationToken>((change, _) =>
            {
                _calls.Add("apply");
                LastChange = change;
                GateWasHeldDuringApply = !_gate.TryEnter(out IDisposable? lease);
                lease?.Dispose();
            })
            .ReturnsAsync(() => new AlbumEditApplied(LastChange!.NewArtistId ?? ArtistId, LastChange.NewGenreId ?? GenreId));
        SetupChecks(path => Check(path));
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
            {
                _calls.Add($"write:{requests[0].FilePath}");

                return (IReadOnlyList<TagWriteResult>)[Written(requests[0])];
            });

        _sut = new AlbumEditService(
            _albums.Object,
            _artists.Object,
            _genres.Object,
            _tracks.Object,
            _editRepository.Object,
            _pending.Object,
            new TrackFileTagWriter(_writer.Object, _player.Object, NullLogger<TrackFileTagWriter>.Instance),
            _import.Object,
            _gate,
            _options.Object,
            NullLogger<AlbumEditService>.Instance);
    }

    private AlbumEditDbChange? LastChange { get; set; }

    private bool GateWasHeldDuringApply { get; set; }

    private AlbumEntity Album { get; set; } = new()
    {
        Id = AlbumId,
        Name = "Old",
        ArtistId = ArtistId,
        ArtistName = "Old Artist",
        GenreId = GenreId,
        GenreName = "Rock",
        Year = 1999,
        TrackCount = 2
    };

    private List<AlbumEntity> AllAlbums { get; set; } = [];

    private List<ArtistEntity> Artists { get; set; } =
    [
        new ArtistEntity { Id = ArtistId, Name = "Old Artist", IsFavorite = true, ListenCount = 7 },
        new ArtistEntity { Id = 11, Name = "Other Artist" }
    ];

    private List<GenreEntity> Genres { get; set; } =
    [
        new GenreEntity { Id = GenreId, Name = "Rock" },
        new GenreEntity { Id = 21, Name = "Jazz" }
    ];

    private List<TrackEntity> TrackList { get; set; } =
    [
        new TrackEntity { Id = 100, AlbumId = AlbumId, ArtistId = ArtistId, GenreId = GenreId, ArtistName = "Old Artist", GenreName = "Rock", MusicFile = @"C:\music\a.flac", Disc = 2 },
        new TrackEntity { Id = 101, AlbumId = AlbumId, ArtistId = ArtistId, GenreId = GenreId, ArtistName = "Old Artist", GenreName = "Rock", MusicFile = @"C:\music\b.flac" }
    ];

    private List<TrackEntity> ArtistTracks { get; set; } = [];

    private List<PendingTagWrite> PendingRows { get; set; } = [];

    private static TagFieldValues Values(string album = "Old", string artist = "Old Artist", int? year = 1999, string genre = "Rock")
    {
        return new TagFieldValues("T", 1, null, "", null) { Album = album, Artist = artist, Year = year, Genre = genre };
    }

    private static TagWriteCheck Check(string path, ETagWriteReason reason = ETagWriteReason.None, TagFieldValues? current = null)
    {
        return new TagWriteCheck(path, reason, "Old Artist") { FileDateModified = FileDate, Size = 99, Current = current ?? Values() };
    }

    private static TagWriteResult Written(TagWriteRequest request)
    {
        TagChanges changes = request.Changes;
        TagFieldValues values = Values(changes.Album ?? "Old", changes.Artist ?? "Old Artist", changes.Year ?? 1999, changes.Genre ?? "Rock");

        return new TagWriteResult(request.FilePath, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = values };
    }

    private void SetupChecks(Func<string, TagWriteCheck> factory)
    {
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) => (IReadOnlyList<TagWriteCheck>)paths.Select(factory).ToList());
    }

    private void VerifyNothingChanged()
    {
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _editRepository.Verify(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<Result<AlbumTagWriteOutcome>> EditAsync(string name = "New", string artist = "Old Artist", int? year = null, string? genre = null)
    {
        return _sut.EditAsync(AlbumId, name, artist, year, genre, null, CancellationToken.None);
    }

    [Fact(DisplayName = "edit_applies_db_before_files_and_completes_each_written_file")]
    public async Task EditAsync_NameChanged_AppliesDatabaseThenWritesAndCompletesEachFile()
    {
        // Arrange
        List<ArtistRenameProgress> reports = [];

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.EditAsync(AlbumId, "  New  ", "Old Artist", null, null, new ReportList(reports), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(["apply", @"write:C:\music\a.flac", "complete:100", @"write:C:\music\b.flac", "complete:101"], _calls);
        Assert.Equal("New", LastChange!.Name);
        Assert.Equal([new PendingTagWrite(100, ETagWriteFields.Album), new PendingTagWrite(101, ETagWriteFields.Album)], LastChange.PendingWrites);
        Assert.Equal([new ArtistRenameProgress(1, 2), new ArtistRenameProgress(2, 2)], reports);
        Assert.Equal([ArtistId], result.Value.UpdatedArtistIds);
    }

    [Fact(DisplayName = "edit_holds_the_gate_until_the_end")]
    public async Task EditAsync_HoldsTheLibraryWriteGateWhileApplying()
    {
        // Act
        await EditAsync();

        // Assert
        Assert.True(GateWasHeldDuringApply);
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }

    [Fact(DisplayName = "edit_stores_the_normalized_name_and_the_final_year_in_the_change")]
    public async Task EditAsync_YearAndName_StoresNormalizedValues()
    {
        // Act
        await EditAsync(" New ", "Old Artist", 2001);

        // Assert
        Assert.Equal("New", LastChange!.Name);
        Assert.Equal(2001, LastChange.Year);
        Assert.Null(LastChange.NewArtistId);
        Assert.Null(LastChange.NewArtistName);
        Assert.Null(LastChange.NewGenreName);
        Assert.Equal(GenreId, LastChange.OldGenreId);
    }

    [Fact(DisplayName = "edit_moves_the_album_to_an_existing_artist_and_genre")]
    public async Task EditAsync_ExistingArtistAndGenre_UsesTheirIds()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "other artist", null, "jazz");

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(11, LastChange!.NewArtistId);
        Assert.Null(LastChange.NewArtistName);
        Assert.Equal(21, LastChange.NewGenreId);
        Assert.Null(LastChange.NewGenreName);
        Assert.Equal([ArtistId, 11], result.Value.UpdatedArtistIds);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.Artist == "Other Artist" && r[0].Changes.Genre == "Jazz"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "edit_creates_the_missing_artist_and_genre")]
    public async Task EditAsync_UnknownArtistAndGenre_AsksTheRepositoryToCreateThem()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "Brand New", null, "Ambient");

        // Assert
        result.Should().BeSuccess();
        Assert.Null(LastChange!.NewArtistId);
        Assert.Equal("Brand New", LastChange.NewArtistName);
        Assert.Null(LastChange.NewGenreId);
        Assert.Equal("Ambient", LastChange.NewGenreName);
    }

    [Fact(DisplayName = "edit_with_a_blocker_writes_nothing")]
    public async Task EditAsync_BlockingFile_WritesNothing()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.flac") ? Check(path, ETagWriteReason.ReadOnly) : Check(path));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == @"C:\music\b.flac");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_blocks_a_file_that_carries_neither_the_old_nor_the_new_name")]
    public async Task EditAsync_OutOfSyncFile_Blocks()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.flac") ? Check(path, current: Values(album: "Something Else")) : Check(path));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.OutOfSync);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_blocks_the_playing_track")]
    public async Task EditAsync_PlayingTrack_Blocks()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 100, MusicFile = @"C:\music\a.flac" });

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.Playing);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_during_import_is_refused")]
    public async Task EditAsync_ImportInProgress_IsRefused()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_is_refused_when_the_library_gate_is_held")]
    public async Task EditAsync_GateHeld_IsRefused()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();
        lease.Dispose();

        // Assert
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_rechecks_under_the_gate")]
    public async Task EditAsync_FileBecomesBlockedAfterThePreview_IsStillBlocked()
    {
        // Arrange
        Result<AlbumEditPreview> preview = await _sut.PreviewAsync(AlbumId, "New", "Old Artist", null, null, CancellationToken.None);
        SetupChecks(path => Check(path, ETagWriteReason.Locked));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Empty(preview.Value.Blockers);
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "a_write_incident_keeps_pending_rows_and_lists_the_file")]
    public async Task EditAsync_OneFileFails_ReturnsPartialAndKeepsItsPendingRow()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("a.flac")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(@"C:\music\a.flac", ETagWriteOutcome.Skipped, ETagWriteReason.Locked, null, null)]);

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        ArtistRenameFailure failure = Assert.Single(result.Value.Failures);
        Assert.Equal(@"C:\music\a.flac", failure.FilePath);
        Assert.Equal(ETagWriteReason.Locked, failure.Reason);
        Assert.DoesNotContain("complete:100", _calls);
        Assert.Contains("complete:101", _calls);
    }

    [Fact(DisplayName = "a_pending_row_that_cannot_be_cleared_makes_the_edit_partial")]
    public async Task EditAsync_CompleteFails_ReturnsPartial()
    {
        // Arrange
        _pending.Setup(r => r.CompleteAsync(100, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("disk"));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(@"C:\music\a.flac", Assert.Single(result.Value.Failures).FilePath);
    }

    [Fact(DisplayName = "a_failing_transaction_returns_failed_and_writes_nothing")]
    public async Task EditAsync_ApplyThrows_ReturnsFailedAndWritesNothing()
    {
        // Arrange
        _editRepository.Setup(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Failed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "edit_refuses_when_writing_tags_is_disabled")]
    public async Task EditAsync_OptionDisabled_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.disabled");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_refuses_an_unknown_album")]
    public async Task EditAsync_UnknownAlbum_ReturnsNotFound()
    {
        // Arrange
        Album = null!;

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("album.not_found");
    }

    [Fact(DisplayName = "edit_refuses_when_nothing_changes")]
    public async Task EditAsync_NothingChanged_ReturnsUnchanged()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("old", "OLD ARTIST", 1999, "rock");

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.unchanged");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_refuses_a_new_artist_for_a_compilation")]
    public async Task EditAsync_CompilationArtistChanged_IsRefused()
    {
        // Arrange
        Album.IsCompilation = true;

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "Other Artist");

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.compilation_artist");
    }

    [Fact(DisplayName = "edit_refuses_while_files_are_waiting_for_a_write")]
    public async Task EditAsync_PendingRows_IsRefused()
    {
        // Arrange
        PendingRows = [new PendingTagWrite(100, ETagWriteFields.Album)];

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.pending");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_refuses_an_artist_with_a_rename_pending")]
    public async Task EditAsync_ArtistRenamePending_IsRefused()
    {
        // Arrange
        Artists[1].RenamePendingFrom = "Previous";

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "Other Artist");

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.artist_pending_rename");
    }

    [Fact(DisplayName = "edit_refuses_a_new_genre_when_no_track_carries_the_old_one")]
    public async Task EditAsync_NewGenreWithoutTrackOfTheOldGenre_IsRefused()
    {
        // Arrange
        foreach (TrackEntity track in TrackList)
            track.GenreId = 21;

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "Old Artist", null, "Ambient");

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.genre_unused");
    }

    [Fact(DisplayName = "edit_refuses_a_genre_change_when_the_import_gives_tracks_the_genre_of_their_artist")]
    public async Task EditAsync_GenreChangeWithArtistGenreOption_IsRefusedAndNothingIsWritten()
    {
        // Arrange
        _options.SetupGet(o => o.ImportTrackWithArtistGenre).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("New", "Old Artist", null, "Jazz");

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.genre_from_artist");
        VerifyNothingChanged();
        _editRepository.Verify(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "preview_refuses_a_genre_change_when_the_import_gives_tracks_the_genre_of_their_artist")]
    public async Task PreviewAsync_GenreChangeWithArtistGenreOption_IsRefused()
    {
        // Arrange
        _options.SetupGet(o => o.ImportTrackWithArtistGenre).Returns(true);

        // Act
        Result<AlbumEditPreview> result = await _sut.PreviewAsync(AlbumId, "New", "Old Artist", null, "Jazz", CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.edit.genre_from_artist");
    }

    [Fact(DisplayName = "edit_without_genre_change_still_works_when_the_import_gives_tracks_the_genre_of_their_artist")]
    public async Task EditAsync_NameChangeWithArtistGenreOption_IsAccepted()
    {
        // Arrange
        _options.SetupGet(o => o.ImportTrackWithArtistGenre).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("New", "Old Artist", null, "Rock");

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Null(LastChange!.NewGenreId);
    }

    [Fact(DisplayName = "edit_accepts_an_existing_genre_when_no_track_carries_the_old_one")]
    public async Task EditAsync_ExistingGenreWithoutTrackOfTheOldGenre_IsAccepted()
    {
        // Arrange
        foreach (TrackEntity track in TrackList)
            track.GenreId = 21;

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync("Old", "Old Artist", null, "Jazz");

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(21, LastChange!.NewGenreId);
        Assert.Empty(LastChange.PendingWrites);
    }

    [Fact(DisplayName = "edit_refuses_a_name_taken_by_another_album_of_the_artist")]
    public async Task EditAsync_NameCollision_ReturnsConflict()
    {
        // Arrange
        AllAlbums = [new AlbumEntity { Id = 2, Name = "NEW", ArtistId = ArtistId, TrackCount = 5 }];

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("album.rename.collision");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "edit_refuses_a_name_taken_by_another_compilation")]
    public async Task EditAsync_CompilationNameCollision_ReturnsConflict()
    {
        // Arrange
        Album.IsCompilation = true;
        AllAlbums = [new AlbumEntity { Id = 2, Name = "New", IsCompilation = true }];

        // Act
        Result<AlbumTagWriteOutcome> result = await EditAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.rename.collision");
    }

    [Fact(DisplayName = "preview_renders_a_collision_without_blocking")]
    public async Task PreviewAsync_NameCollision_RendersTheCollision()
    {
        // Arrange
        AllAlbums = [new AlbumEntity { Id = 2, Name = "New", ArtistId = ArtistId, TrackCount = 5 }, new AlbumEntity { Id = 3, Name = "New", ArtistId = ArtistId, IsCompilation = true }];

        // Act
        Result<AlbumEditPreview> result = await _sut.PreviewAsync(AlbumId, "New", "Old Artist", null, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(new AlbumCollision(2, "New", "Old Artist", 5), result.Value.Collision);
    }

    [Fact(DisplayName = "preview_counts_files_genre_tracks_and_other_artist_tracks")]
    public async Task PreviewAsync_ArtistAndGenreChange_CountsTracks()
    {
        // Arrange
        TrackList[1].GenreId = 21;
        TrackList[1].ArtistId = 11;

        // Act
        Result<AlbumEditPreview> result = await _sut.PreviewAsync(AlbumId, "Old", "Other Artist", 2001, "Jazz", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        AlbumEditPreview preview = result.Value;
        Assert.Equal("Old Artist", preview.OldArtist);
        Assert.Equal("Other Artist", preview.NewArtist);
        Assert.False(preview.CreatesArtist);
        Assert.Equal(1999, preview.OldYear);
        Assert.Equal(2001, preview.NewYear);
        Assert.Equal("Rock", preview.OldGenre);
        Assert.Equal("Jazz", preview.NewGenre);
        Assert.False(preview.CreatesGenre);
        Assert.Equal(1, preview.GenreTrackCount);
        Assert.Equal(1, preview.GenreKeptCount);
        Assert.Equal(1, preview.OtherArtistTrackCount);
        Assert.Equal(2, preview.FileCount);
        Assert.Empty(preview.Blockers);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "preview_warns_when_the_old_artist_keeps_no_track")]
    public async Task PreviewAsync_ArtistLeavesWithItsOnlyAlbum_WarnsAboutTheOrphan()
    {
        // Arrange
        ArtistTracks = [.. TrackList];

        // Act
        Result<AlbumEditPreview> result = await _sut.PreviewAsync(AlbumId, "Old", "Brand New", null, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.True(result.Value.CreatesArtist);
        Assert.Equal(new OrphanArtistWarning(ArtistId, "Old Artist", true, 7), result.Value.OrphanArtist);
    }

    [Fact(DisplayName = "preview_does_not_warn_when_the_old_artist_keeps_another_album")]
    public async Task PreviewAsync_ArtistKeepsAnotherAlbum_NoOrphanWarning()
    {
        // Arrange
        ArtistTracks = [.. TrackList, new TrackEntity { Id = 300, AlbumId = 2, ArtistId = ArtistId }];

        // Act
        Result<AlbumEditPreview> result = await _sut.PreviewAsync(AlbumId, "Old", "Other Artist", null, null, CancellationToken.None);

        // Assert
        Assert.Null(result.Value.OrphanArtist);
    }

    [Fact(DisplayName = "resume_rewrites_pending_files_from_database_values")]
    public async Task ResumeAsync_PendingRows_WritesDatabaseValuesAndClearsRows()
    {
        // Arrange
        Album.Name = "New";
        Album.Year = 2001;
        TrackList[0].GenreName = "Jazz";
        PendingRows = [new PendingTagWrite(100, ETagWriteFields.Album | ETagWriteFields.Genre | ETagWriteFields.Disc), new PendingTagWrite(101, ETagWriteFields.Year)];
        SetupChecks(path => Check(path, current: Values(album: "Old")));

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(["orphans", @"write:C:\music\a.flac", "complete:100", @"write:C:\music\b.flac", "complete:101"], _calls);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r =>
            r[0].FilePath.EndsWith("a.flac") && r[0].Changes.Album == "New" && r[0].Changes.Genre == "Jazz" && r[0].Changes.Disc == 2 && r[0].Changes.Year == null && r[0].Changes.Artist == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r =>
            r[0].FilePath.EndsWith("b.flac") && r[0].Changes.Year == 2001 && r[0].Changes.Album == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _editRepository.Verify(r => r.ApplyAsync(It.IsAny<AlbumEditDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "resume_completes_a_file_that_already_carries_the_values_without_writing")]
    public async Task ResumeAsync_FileAlreadyUpToDate_CompletesWithoutWriting()
    {
        // Arrange
        Album.Name = "New";
        PendingRows = [new PendingTagWrite(101, ETagWriteFields.Album)];
        SetupChecks(path => Check(path, current: Values(album: "New")));

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(["orphans", "complete:101"], _calls);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "resume_never_blocks_on_an_out_of_sync_file")]
    public async Task ResumeAsync_FileWithAnotherName_IsStillWritten()
    {
        // Arrange
        Album.Name = "New";
        PendingRows = [new PendingTagWrite(101, ETagWriteFields.Album)];
        SetupChecks(path => Check(path, current: Values(album: "Edited by hand")));

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Contains(@"write:C:\music\b.flac", _calls);
    }

    [Fact(DisplayName = "resume_stays_partial_when_a_file_still_fails")]
    public async Task ResumeAsync_FileFails_ReturnsPartial()
    {
        // Arrange
        Album.Name = "New";
        PendingRows = [new PendingTagWrite(101, ETagWriteFields.Album)];
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(@"C:\music\b.flac", ETagWriteOutcome.Skipped, ETagWriteReason.ReadOnly, null, null)]);

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(ETagWriteReason.ReadOnly, Assert.Single(result.Value.Failures).Reason);
        Assert.DoesNotContain("complete:101", _calls);
    }

    [Fact(DisplayName = "resume_is_blocked_by_an_unwritable_file_and_writes_nothing")]
    public async Task ResumeAsync_BlockingFile_WritesNothing()
    {
        // Arrange
        PendingRows = [new PendingTagWrite(101, ETagWriteFields.Album)];
        SetupChecks(path => Check(path, ETagWriteReason.ReadOnly));

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "resume_without_pending_rows_is_completed")]
    public async Task ResumeAsync_NoPendingRows_IsCompleted()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "resume_is_refused_during_an_import")]
    public async Task ResumeAsync_ImportInProgress_IsRefused()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
    }

    [Fact(DisplayName = "resume_refuses_when_writing_tags_is_disabled_or_the_album_is_unknown")]
    public async Task ResumeAsync_DisabledOrUnknown_ReturnsFailure()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<AlbumTagWriteOutcome> disabled = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        Album = null!;
        Result<AlbumTagWriteOutcome> unknown = await _sut.ResumeAsync(AlbumId, null, CancellationToken.None);

        // Assert
        disabled.Should().BeFailure().And.HaveErrorWithCode("album.edit.disabled");
        unknown.Should().BeFailure().And.HaveErrorWithCode("album.not_found");
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}