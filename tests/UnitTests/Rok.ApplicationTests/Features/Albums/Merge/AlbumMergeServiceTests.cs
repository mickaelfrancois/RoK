using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Errors;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.Albums.Merge;

public class AlbumMergeServiceTests
{
    private const long SourceId = 1;
    private const long TargetId = 2;
    private const long SourceArtistId = 10;
    private const long TargetArtistId = 11;

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumMergeRepository> _mergeRepository = new();
    private readonly Mock<IPendingTagWriteRepository> _pending = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly List<string> _calls = [];
    private readonly AlbumMergeService _sut;

    public AlbumMergeServiceTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _albums.Setup(r => r.GetByIdAsync(SourceId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Source);
        _albums.Setup(r => r.GetByIdAsync(TargetId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Target);
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Artists);
        _tracks.Setup(r => r.GetByAlbumIdAsync(SourceId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => SourceTracks);
        _tracks.Setup(r => r.GetByAlbumIdAsync(TargetId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => TargetTracks);
        _tracks.Setup(r => r.GetByArtistIdAsync(SourceArtistId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => SourceArtistTracks);
        _pending.Setup(r => r.CountByAlbumAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())).ReturnsAsync((long id, CancellationToken _) => PendingCounts.GetValueOrDefault(id));
        _pending.Setup(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Callback<long, DateTime, long, CancellationToken>((id, _, _, _) => _calls.Add($"complete:{id}"))
            .Returns(Task.CompletedTask);
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<AlbumMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<AlbumMergeDbChange, CancellationToken>((change, _) =>
            {
                _calls.Add("apply");
                LastChange = change;
                GateWasHeldDuringApply = !_gate.TryEnter(out IDisposable? lease);
                lease?.Dispose();
            })
            .Returns(Task.CompletedTask);
        SetupChecks(path => Check(path));
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
            {
                _calls.Add($"write:{requests[0].FilePath}");

                return (IReadOnlyList<TagWriteResult>)[Written(requests[0])];
            });

        _sut = new AlbumMergeService(
            _albums.Object,
            _artists.Object,
            _tracks.Object,
            _mergeRepository.Object,
            _pending.Object,
            new TrackFileTagWriter(_writer.Object, _player.Object, NullLogger<TrackFileTagWriter>.Instance),
            _import.Object,
            _gate,
            _options.Object,
            NullLogger<AlbumMergeService>.Instance);
    }

    private AlbumMergeDbChange? LastChange { get; set; }

    private bool GateWasHeldDuringApply { get; set; }

    private Dictionary<long, int> PendingCounts { get; } = [];

    private AlbumEntity Source { get; set; } = new()
    {
        Id = SourceId,
        Name = "Src",
        ArtistId = SourceArtistId,
        ArtistName = "Source Artist",
        AlbumPath = @"C:\music\src",
        Year = 2000
    };

    private AlbumEntity Target { get; set; } = new()
    {
        Id = TargetId,
        Name = "Target",
        ArtistId = SourceArtistId,
        ArtistName = "Source Artist",
        AlbumPath = @"C:\music\src",
        Year = 1999
    };

    private List<ArtistEntity> Artists { get; set; } =
    [
        new ArtistEntity { Id = SourceArtistId, Name = "Source Artist", IsFavorite = true, ListenCount = 7 },
        new ArtistEntity { Id = TargetArtistId, Name = "Target Artist" }
    ];

    private List<TrackEntity> SourceTracks { get; set; } =
    [
        new TrackEntity { Id = 100, AlbumId = SourceId, ArtistId = SourceArtistId, MusicFile = @"C:\music\src\a.flac", TrackNumber = 1 },
        new TrackEntity { Id = 101, AlbumId = SourceId, ArtistId = SourceArtistId, MusicFile = @"C:\music\src\b.flac", TrackNumber = 2 }
    ];

    private List<TrackEntity> TargetTracks { get; set; } =
    [
        new TrackEntity { Id = 200, AlbumId = TargetId, ArtistId = SourceArtistId, MusicFile = @"C:\music\src\c.flac", TrackNumber = 3 },
        new TrackEntity { Id = 201, AlbumId = TargetId, ArtistId = SourceArtistId, MusicFile = @"C:\music\src\d.flac", TrackNumber = 4, Disc = 2 }
    ];

    private List<TrackEntity> SourceArtistTracks { get; set; } = [];

    private static TagFieldValues Values(string album = "Src", string artist = "Source Artist", int? disc = null)
    {
        return new TagFieldValues("T", 1, disc, "", null) { Album = album, Artist = artist, Year = 2000, Genre = "Rock" };
    }

    private static TagWriteCheck Check(string path, ETagWriteReason reason = ETagWriteReason.None, TagFieldValues? current = null)
    {
        return new TagWriteCheck(path, reason, "Source Artist") { FileDateModified = FileDate, Size = 99, Current = current ?? Values() };
    }

    private static TagWriteResult Written(TagWriteRequest request)
    {
        TagChanges changes = request.Changes;
        TagFieldValues values = Values(changes.Album ?? "Src", changes.Artist ?? "Source Artist", changes.Disc);

        return new TagWriteResult(request.FilePath, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = values };
    }

    private void SetupChecks(Func<string, TagWriteCheck> factory)
    {
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) => (IReadOnlyList<TagWriteCheck>)paths.Select(factory).ToList());
    }

    private void MoveTargetToAnotherArtist()
    {
        Target.ArtistId = TargetArtistId;
        Target.ArtistName = "Target Artist";
        foreach (TrackEntity track in TargetTracks)
            track.ArtistId = TargetArtistId;
    }

    private void CollideOnFirstTrack()
    {
        TargetTracks[0].TrackNumber = 1;
    }

    private void VerifyNothingChanged()
    {
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mergeRepository.Verify(r => r.ApplyAsync(It.IsAny<AlbumMergeDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private Task<Result<AlbumTagWriteOutcome>> MergeAsync(bool shiftDiscs = false)
    {
        return _sut.MergeAsync(SourceId, TargetId, shiftDiscs, null, CancellationToken.None);
    }

    [Fact(DisplayName = "merge_applies_db_before_files_and_completes_each_written_file")]
    public async Task MergeAsync_SameArtist_AppliesDatabaseThenWritesAndCompletesEachFile()
    {
        // Arrange
        List<ArtistRenameProgress> reports = [];

        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.MergeAsync(SourceId, TargetId, false, new ReportList(reports), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(TargetId, result.Value.AlbumId);
        Assert.Equal(["apply", @"write:C:\music\src\a.flac", "complete:100", @"write:C:\music\src\b.flac", "complete:101"], _calls);
        Assert.Equal([new PendingTagWrite(100, ETagWriteFields.Album), new PendingTagWrite(101, ETagWriteFields.Album)], LastChange!.PendingWrites);
        Assert.Equal([new ArtistRenameProgress(1, 2), new ArtistRenameProgress(2, 2)], reports);
        Assert.Equal([SourceArtistId], result.Value.UpdatedArtistIds);
        Assert.Equal([SourceId], result.Value.DeletedAlbumIds);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.Album == "Target" && r[0].Changes.Artist == null && r[0].Changes.Disc == null), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "merge_stores_the_merged_values_and_no_offset_in_the_change")]
    public async Task MergeAsync_NoConflict_StoresMergedAlbumWithoutOffset()
    {
        // Act
        await MergeAsync();

        // Assert
        Assert.Equal(SourceId, LastChange!.SourceId);
        Assert.Equal(TargetId, LastChange.TargetId);
        Assert.Equal(TargetId, LastChange.Merged.Id);
        Assert.Equal(1999, LastChange.Merged.Year);
        Assert.Equal(0, LastChange.DiscOffset);
    }

    [Fact(DisplayName = "merge_holds_the_gate_until_the_end")]
    public async Task MergeAsync_HoldsTheLibraryWriteGateWhileApplying()
    {
        // Act
        await MergeAsync();

        // Assert
        Assert.True(GateWasHeldDuringApply);
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }

    [Fact(DisplayName = "merge_gives_the_tracks_the_artist_of_the_kept_album")]
    public async Task MergeAsync_DifferentArtist_WritesTheArtistAndReportsBothArtists()
    {
        // Arrange
        MoveTargetToAnotherArtist();

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(TargetArtistId, LastChange!.Merged.ArtistId);
        Assert.Equal([new PendingTagWrite(100, ETagWriteFields.Album | ETagWriteFields.Artist), new PendingTagWrite(101, ETagWriteFields.Album | ETagWriteFields.Artist)], LastChange.PendingWrites);
        Assert.Equal([SourceArtistId, TargetArtistId], result.Value.UpdatedArtistIds);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.Artist == "Target Artist"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "merge_does_not_rewrite_a_file_that_already_carries_the_kept_name")]
    public async Task MergeAsync_FileAlreadyCarriesTargetName_IsSkipped()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("a.flac") ? Check(path, current: Values(album: "target")) : Check(path));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeSuccess();
        Assert.Equal([new PendingTagWrite(101, ETagWriteFields.Album)], LastChange!.PendingWrites);
        Assert.Equal(["apply", @"write:C:\music\src\b.flac", "complete:101"], _calls);
    }

    [Fact(DisplayName = "merge_refuses_colliding_track_numbers_without_the_shift")]
    public async Task MergeAsync_ConflictWithoutShift_IsRefusedAndChangesNothing()
    {
        // Arrange
        CollideOnFirstTrack();

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync(false);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.conflicts");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_shifts_the_discs_after_the_last_disc_of_the_kept_album_when_asked")]
    public async Task MergeAsync_ConflictWithShift_WritesTheShiftedDiscs()
    {
        // Arrange
        CollideOnFirstTrack();

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync(true);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(2, LastChange!.DiscOffset);
        Assert.Equal([new PendingTagWrite(100, ETagWriteFields.Album | ETagWriteFields.Disc), new PendingTagWrite(101, ETagWriteFields.Album | ETagWriteFields.Disc)], LastChange.PendingWrites);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].Changes.Disc == 3), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "merge_ignores_the_shift_when_nothing_collides")]
    public async Task MergeAsync_ShiftAskedWithoutConflict_KeepsTheDiscs()
    {
        // Act
        await MergeAsync(true);

        // Assert
        Assert.Equal(0, LastChange!.DiscOffset);
        Assert.All(LastChange.PendingWrites, p => Assert.Equal(ETagWriteFields.Album, p.Fields));
    }

    [Fact(DisplayName = "merge_refuses_a_compilation")]
    public async Task MergeAsync_CompilationOnEitherSide_IsRefused()
    {
        // Arrange
        Source.IsCompilation = true;

        // Act
        Result<AlbumTagWriteOutcome> sourceResult = await MergeAsync();

        Source.IsCompilation = false;
        Target.IsCompilation = true;
        Result<AlbumTagWriteOutcome> targetResult = await MergeAsync();

        // Assert
        sourceResult.Should().BeFailure().And.HaveErrorWithCode("album.merge.compilation");
        targetResult.Should().BeFailure().And.HaveErrorWithCode("album.merge.compilation");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_refuses_an_album_merged_into_itself")]
    public async Task MergeAsync_SameAlbum_IsRefused()
    {
        // Act
        Result<AlbumTagWriteOutcome> result = await _sut.MergeAsync(SourceId, SourceId, false, null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.same_album");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_is_refused_when_writing_tags_is_disabled")]
    public async Task MergeAsync_OptionDisabled_IsRefused()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.disabled");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_returns_not_found_for_an_unknown_album")]
    public async Task MergeAsync_UnknownAlbum_ReturnsNotFound()
    {
        // Arrange
        Target = null!;

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.not_found");
        VerifyNothingChanged();
    }

    [Theory(DisplayName = "merge_is_refused_while_an_album_waits_for_a_write")]
    [InlineData(SourceId)]
    [InlineData(TargetId)]
    public async Task MergeAsync_PendingWrite_IsRefused(long albumId)
    {
        // Arrange
        PendingCounts[albumId] = 1;

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.pending");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_is_refused_while_an_artist_rename_is_pending")]
    public async Task MergeAsync_ArtistRenamePending_IsRefused()
    {
        // Arrange
        Artists[0].RenamePendingFrom = "Former";

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.artist_pending_rename");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_with_a_blocker_writes_nothing")]
    public async Task MergeAsync_BlockingFile_WritesNothing()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.flac") ? Check(path, ETagWriteReason.ReadOnly) : Check(path));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == @"C:\music\src\b.flac");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_blocks_a_file_that_carries_neither_album_name")]
    public async Task MergeAsync_OutOfSyncFile_Blocks()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.flac") ? Check(path, current: Values(album: "Something Else")) : Check(path));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.OutOfSync);
        VerifyNothingChanged();
    }

    [Theory(DisplayName = "merge_blocks_when_a_track_of_either_album_is_playing")]
    [InlineData(100, @"C:\music\src\a.flac")]
    [InlineData(200, @"C:\music\src\c.flac")]
    public async Task MergeAsync_PlayingTrack_Blocks(long id, string file)
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = id, MusicFile = file });

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Contains(result.Value.Blockers, b => b.Reason == EArtistRenameBlock.Playing);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_during_import_is_refused")]
    public async Task MergeAsync_ImportInProgress_IsRefused()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_is_refused_when_the_library_gate_is_held")]
    public async Task MergeAsync_GateHeld_IsRefused()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();
        lease.Dispose();

        // Assert
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_reports_a_failed_state_and_writes_nothing_when_the_transaction_fails")]
    public async Task MergeAsync_TransactionFails_ReturnsFailedAndWritesNothing()
    {
        // Arrange
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<AlbumMergeDbChange>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Failed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "merge_is_partial_when_a_file_cannot_be_written_and_keeps_its_pending_row")]
    public async Task MergeAsync_OneFileFails_IsPartial()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("b.flac")), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
                (IReadOnlyList<TagWriteResult>)[new TagWriteResult(requests[0].FilePath, ETagWriteOutcome.Failed, ETagWriteReason.Locked, null, null)]);

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(@"C:\music\src\b.flac", Assert.Single(result.Value.Failures).FilePath);
        Assert.Equal([SourceId], result.Value.DeletedAlbumIds);
        Assert.DoesNotContain("complete:101", _calls);
        Assert.Contains("complete:100", _calls);
    }

    [Fact(DisplayName = "merge_lists_a_file_whose_pending_row_cannot_be_cleared")]
    public async Task MergeAsync_CompleteFails_ListsTheFile()
    {
        // Arrange
        _pending.Setup(r => r.CompleteAsync(101, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        Result<AlbumTagWriteOutcome> result = await MergeAsync();

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(@"C:\music\src\b.flac", Assert.Single(result.Value.Failures).FilePath);
    }

    [Fact(DisplayName = "preview_lists_the_files_conflicts_and_changes_nothing")]
    public async Task PreviewAsync_Conflicts_ListsEverythingAndChangesNothing()
    {
        // Arrange
        MoveTargetToAnotherArtist();
        CollideOnFirstTrack();
        SourceTracks[0].Title = "Src One";
        TargetTracks[0].Title = "Target One";

        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        AlbumMergePreview preview = result.Value;
        Assert.Equal(2, preview.FileCount);
        Assert.Equal("Src", preview.SourceName);
        Assert.Equal("Target", preview.TargetName);
        Assert.Equal("Source Artist", preview.SourceArtist);
        Assert.Equal("Target Artist", preview.TargetArtist);
        Assert.True(preview.ArtistChanges);
        Assert.Equal(2, preview.SourceTrackCount);
        Assert.Equal(2, preview.TargetTrackCount);
        Assert.Equal(new DiscConflict(1, 1, "Src One", "Target One"), Assert.Single(preview.DiscConflicts));
        Assert.Equal(2, preview.SuggestedDiscOffset);
        Assert.Equal("Year", Assert.Single(preview.FieldConflicts).Field);
        Assert.Empty(preview.Blockers);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "preview_suggests_no_offset_when_nothing_collides")]
    public async Task PreviewAsync_NoCollision_SuggestsNoOffset()
    {
        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        Assert.Empty(result.Value.DiscConflicts);
        Assert.Equal(0, result.Value.SuggestedDiscOffset);
        Assert.False(result.Value.ArtistChanges);
    }

    [Fact(DisplayName = "preview_flags_distinct_folders")]
    public async Task PreviewAsync_TrackOutsideTheKeptFolder_FlagsSeparateFolders()
    {
        // Arrange
        Target.AlbumPath = @"C:\music\target\";

        // Act
        Result<AlbumMergePreview> separate = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        Target.AlbumPath = @"C:\MUSIC\src\";
        Result<AlbumMergePreview> same = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        Assert.True(separate.Value.SeparateFolders);
        Assert.False(same.Value.SeparateFolders);
    }

    [Fact(DisplayName = "preview_warns_when_the_merged_artist_keeps_no_track")]
    public async Task PreviewAsync_SourceArtistWithoutOtherTrack_ReportsOrphan()
    {
        // Arrange
        MoveTargetToAnotherArtist();
        SourceArtistTracks = [.. SourceTracks];

        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        OrphanArtistWarning orphan = Assert.IsType<OrphanArtistWarning>(result.Value.OrphanArtist);
        Assert.Equal(SourceArtistId, orphan.ArtistId);
        Assert.True(orphan.IsFavorite);
        Assert.Equal(7, orphan.ListenCount);
    }

    [Fact(DisplayName = "preview_does_not_warn_when_the_merged_artist_keeps_another_track")]
    public async Task PreviewAsync_SourceArtistWithOtherTrack_ReportsNoOrphan()
    {
        // Arrange
        MoveTargetToAnotherArtist();
        SourceArtistTracks = [.. SourceTracks, new TrackEntity { Id = 900, AlbumId = 77, ArtistId = SourceArtistId }];

        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        Assert.Null(result.Value.OrphanArtist);
    }

    [Fact(DisplayName = "preview_lists_the_blockers")]
    public async Task PreviewAsync_BlockingFile_ListsTheBlocker()
    {
        // Arrange
        SetupChecks(path => Check(path, ETagWriteReason.Locked));

        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Value.Blockers.Count);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "preview_refuses_a_compilation")]
    public async Task PreviewAsync_Compilation_IsRefused()
    {
        // Arrange
        Target.IsCompilation = true;

        // Act
        Result<AlbumMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("album.merge.compilation");
    }

    private sealed class ReportList(List<ArtistRenameProgress> reports) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => reports.Add(value);
    }
}