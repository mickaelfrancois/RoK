using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Features.Artists.Rename;

public class ArtistRenameServiceTests
{
    private const long ArtistId = 1;
    private const string OldName = "Old Band";
    private const string NewName = "New Band";

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IPlaylistHeaderRepository> _playlists = new();
    private readonly Mock<IArtistRenameRepository> _renameRepository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IArtistPicture> _picture = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly List<string> _calls = [];
    private readonly List<TrackEntity> _trackList;
    private readonly ArtistRenameService _sut;

    public ArtistRenameServiceTests()
    {
        _trackList =
        [
            new TrackEntity { Id = 10, AlbumId = 100, MusicFile = @"C:\music\a.mp3" },
            new TrackEntity { Id = 11, AlbumId = 100, MusicFile = @"C:\music\b.mp3" },
            new TrackEntity { Id = 12, AlbumId = 101, MusicFile = @"C:\music\c.flac" }
        ];

        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Artists);
        _tracks.Setup(r => r.GetByArtistIdAsync(ArtistId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => _trackList);
        _tracks.Setup(r => r.UpdateFileInfoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<long, DateTime, long, RepositoryConnectionKind>((id, _, _, _) => _calls.Add($"info:{id}"))
            .ReturnsAsync(true);
        _playlists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => PlaylistList);
        _renameRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistRenameDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistRenameDbChange, CancellationToken>((_, _) => _calls.Add("apply"))
            .Returns(Task.CompletedTask);
        _renameRepository.Setup(r => r.CompleteAsync(ArtistId, It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((_, _) => _calls.Add("complete"))
            .ReturnsAsync(true);
        _picture.Setup(p => p.CheckFolderMove(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>())).Returns(ArtistFolderMoveOutcome.NothingToDo);
        _picture.Setup(p => p.MoveFolder(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((from, to) => _calls.Add($"move:{from}>{to}"))
            .Returns(ArtistFolderMoveOutcome.Move);
        _picture.Setup(p => p.GetArtistFolder(It.IsAny<string>())).Returns<string>(name => $@"C:\cache\@Artists\{name}");
        SetupChecks(_ => new TagWriteCheck(_, ETagWriteReason.None, OldName) { FileDateModified = FileDate, Size = 99 });
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
            {
                _calls.Add($"write:{requests[0].FilePath}");

                return (IReadOnlyList<TagWriteResult>)[Written(requests[0].FilePath, requests[0].Changes.Artist!)];
            });

        _sut = new ArtistRenameService(
            _artists.Object,
            _tracks.Object,
            _playlists.Object,
            _renameRepository.Object,
            new ArtistFileRewriter(_writer.Object, _tracks.Object, _player.Object, NullLogger<ArtistFileRewriter>.Instance),
            _picture.Object,
            _import.Object,
            _gate,
            _options.Object,
            NullLogger<ArtistRenameService>.Instance);
    }

    private List<ArtistEntity> Artists { get; set; } =
    [
        new ArtistEntity { Id = ArtistId, Name = OldName },
        new ArtistEntity { Id = 2, Name = "Other Band", AlbumCount = 3, TrackCount = 30 }
    ];

    private List<PlaylistHeaderEntity> PlaylistList { get; set; } = [];

    private void SetupChecks(Func<string, TagWriteCheck> factory)
    {
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) => (IReadOnlyList<TagWriteCheck>)paths.Select(factory).ToList());
    }

    private static TagWriteResult Written(string path, string artist)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = new TagFieldValues("T", 1, null, "", null) { Artist = artist } };
    }

    private static TagWriteResult Skipped(string path, ETagWriteReason reason)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Skipped, reason, null, null);
    }

    private void VerifyNothingChanged()
    {
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _renameRepository.Verify(r => r.ApplyAsync(It.IsAny<ArtistRenameDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
        _picture.Verify(p => p.MoveFolder(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "preview_counts_files_and_albums_and_lists_blockers")]
    public async Task PreviewAsync_CountsFilesAndAlbums_AndListsBlockers()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 10, MusicFile = @"C:\music\a.mp3" });
        SetupChecks(path => path.EndsWith("b.mp3")
            ? new TagWriteCheck(path, ETagWriteReason.ReadOnly, null)
            : path.EndsWith("c.flac")
                ? new TagWriteCheck(path, ETagWriteReason.None, "Somebody Else") { FileDateModified = FileDate, Size = 1 }
                : new TagWriteCheck(path, ETagWriteReason.None, OldName) { FileDateModified = FileDate, Size = 1 });

        // Act
        Result<ArtistRenamePreview> result = await _sut.PreviewAsync(ArtistId, NewName, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        ArtistRenamePreview preview = result.Value;
        Assert.Equal(3, preview.FileCount);
        Assert.Equal(2, preview.AlbumCount);
        Assert.Equal(OldName, preview.OldName);
        Assert.Equal(NewName, preview.NewName);
        Assert.False(preview.IsResume);
        Assert.Null(preview.Collision);
        Assert.Contains(preview.Blockers, b => b.Reason == EArtistRenameBlock.Playing && b.FilePath == @"C:\music\a.mp3");
        Assert.Contains(preview.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == @"C:\music\b.mp3");
        Assert.Contains(preview.Blockers, b => b.Reason == EArtistRenameBlock.OutOfSync && b.Detail == "Somebody Else");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "preview_reports_a_collision_with_the_counts_of_the_other_artist")]
    public async Task PreviewAsync_Collision_ReportsCounts()
    {
        // Act
        Result<ArtistRenamePreview> result = await _sut.PreviewAsync(ArtistId, "other band", CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(new ArtistRenameCollision("Other Band", 3, 30) { ArtistId = 2 }, result.Value.Collision);
        _writer.Verify(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "preview_blocks_when_the_picture_folder_belongs_to_another_artist")]
    public async Task PreviewAsync_PictureFolderBlocked_ListsBlocker()
    {
        // Arrange
        _picture.Setup(p => p.CheckFolderMove(OldName, NewName, It.IsAny<IReadOnlyCollection<string>>())).Returns(ArtistFolderMoveOutcome.Blocked);

        // Act
        Result<ArtistRenamePreview> result = await _sut.PreviewAsync(ArtistId, NewName, CancellationToken.None);

        // Assert
        ArtistRenameBlocker blocker = Assert.Single(result.Value.Blockers);
        Assert.Equal(EArtistRenameBlock.PictureFolder, blocker.Reason);
        _picture.Verify(p => p.CheckFolderMove(OldName, NewName, It.Is<IReadOnlyCollection<string>>(names => names.Single() == "Other Band")), Times.Once);
    }

    [Fact(DisplayName = "the_name_is_normalized_like_the_import_before_use")]
    public async Task PreviewAsync_NormalizesTheName()
    {
        // Act
        Result<ArtistRenamePreview> result = await _sut.PreviewAsync(ArtistId, "  New\u00A0Band  ", CancellationToken.None);

        // Assert
        Assert.Equal(NewName, result.Value.NewName);
    }

    [Fact(DisplayName = "rename_with_a_blocker_writes_nothing")]
    public async Task RenameAsync_WithBlocker_WritesNothing()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.mp3")
            ? new TagWriteCheck(path, ETagWriteReason.Locked, null)
            : new TagWriteCheck(path, ETagWriteReason.None, OldName) { FileDateModified = FileDate, Size = 1 });

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.Locked, Assert.Single(result.Value.Blockers).Reason);
        Assert.Equal(OldName, result.Value.Name);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "rename_during_import_is_refused")]
    public async Task RenameAsync_ImportInProgress_IsBlocked()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "rename_when_the_gate_is_held_is_refused")]
    public async Task RenameAsync_GateHeld_IsBlocked()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));

        // Act
        Result<ArtistRenameOutcome> result;

        using (lease)
            result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "the_gate_is_released_after_the_rename")]
    public async Task RenameAsync_ReleasesTheGate()
    {
        // Act
        await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }

    [Fact(DisplayName = "rename_rechecks_under_the_gate")]
    public async Task RenameAsync_RechecksUnderTheGate()
    {
        // Arrange
        Result<ArtistRenamePreview> preview = await _sut.PreviewAsync(ArtistId, NewName, CancellationToken.None);
        Assert.Empty(preview.Value.Blockers);
        SetupChecks(path => new TagWriteCheck(path, ETagWriteReason.ReadOnly, null));

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(3, result.Value.Blockers.Count);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "rename_applies_db_before_files_and_clears_the_marker_at_the_end")]
    public async Task RenameAsync_AppliesDbBeforeFiles_AndClearsTheMarkerAtTheEnd()
    {
        // Arrange
        List<ArtistRenameProgress> reports = [];
        SyncProgress progress = new(reports.Add);

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, progress, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Equal(NewName, result.Value.Name);
        Assert.Equal(
            [
                $"move:{OldName}>{NewName}",
                "apply",
                @"write:C:\music\a.mp3",
                "info:10",
                @"write:C:\music\b.mp3",
                "info:11",
                @"write:C:\music\c.flac",
                "info:12",
                "complete"
            ],
            _calls);
        Assert.Equal([new ArtistRenameProgress(1, 3), new ArtistRenameProgress(2, 3), new ArtistRenameProgress(3, 3)], reports);
        _renameRepository.Verify(r => r.ApplyAsync(It.Is<ArtistRenameDbChange>(c => c.ArtistId == ArtistId && c.OldName == OldName && c.NewName == NewName), It.IsAny<CancellationToken>()), Times.Once);
        _tracks.Verify(r => r.UpdateFileInfoAsync(10, FileDate.DateTime, 100, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "rename_sends_the_new_name_as_the_artist_change_only")]
    public async Task RenameAsync_SendsOnlyTheArtistChange()
    {
        // Act
        await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r.Count == 1 && r[0].Changes == new TagChanges { Artist = NewName }), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "a_write_incident_keeps_the_marker_and_lists_the_file")]
    public async Task RenameAsync_WriteIncident_ReturnsPartial()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("b.mp3")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Skipped(@"C:\music\b.mp3", ETagWriteReason.Locked)]);

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(NewName, result.Value.Name);
        Assert.Equal(new ArtistRenameFailure(@"C:\music\b.mp3", ETagWriteReason.Locked), Assert.Single(result.Value.Failures));
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _tracks.Verify(r => r.UpdateFileInfoAsync(11, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "a_write_that_reads_back_another_artist_is_an_incident")]
    public async Task RenameAsync_ReadBackMismatch_IsListed()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("a.mp3")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Written(@"C:\music\a.mp3", "Wrong")]);

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(ETagWriteReason.VerificationFailed, Assert.Single(result.Value.Failures).Reason);
    }

    [Fact(DisplayName = "db_failure_moves_the_folder_back_and_writes_nothing")]
    public async Task RenameAsync_DbFailure_MovesTheFolderBack()
    {
        // Arrange
        _renameRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistRenameDbChange>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Failed, result.Value.State);
        Assert.Equal(OldName, result.Value.Name);
        _picture.Verify(p => p.MoveFolder(NewName, OldName), Times.Once);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "a_folder_move_refused_by_the_disk_blocks_the_rename_before_the_database")]
    public async Task RenameAsync_FolderMoveBlocked_ChangesNothingElse()
    {
        // Arrange
        _picture.Setup(p => p.MoveFolder(It.IsAny<string>(), It.IsAny<string>())).Throws(new IOException("in use"));

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.PictureFolder, Assert.Single(result.Value.Blockers).Reason);
        _renameRepository.Verify(r => r.ApplyAsync(It.IsAny<ArtistRenameDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "resume_writes_only_remaining_files")]
    public async Task RenameAsync_Resume_WritesOnlyRemainingFiles()
    {
        // Arrange
        Artists = [new ArtistEntity { Id = ArtistId, Name = NewName, RenamePendingFrom = OldName }];
        SetupChecks(path => path.EndsWith("c.flac")
            ? new TagWriteCheck(path, ETagWriteReason.None, OldName) { FileDateModified = FileDate, Size = 7 }
            : new TagWriteCheck(path, ETagWriteReason.None, NewName) { FileDateModified = FileDate, Size = 5 });

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath == @"C:\music\c.flac"), It.IsAny<CancellationToken>()), Times.Once);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        _tracks.Verify(r => r.UpdateFileInfoAsync(10, FileDate.DateTime, 5, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _tracks.Verify(r => r.UpdateFileInfoAsync(11, FileDate.DateTime, 5, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _renameRepository.Verify(r => r.ApplyAsync(It.Is<ArtistRenameDbChange>(c => c.OldName == OldName && c.NewName == NewName), It.IsAny<CancellationToken>()), Times.Once);
        _renameRepository.Verify(r => r.CompleteAsync(ArtistId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "resume_does_not_rewrite_smart_playlist_criteria_again")]
    public async Task RenameAsync_Resume_DoesNotRewriteCriteriaAgain()
    {
        // Arrange
        Artists = [new ArtistEntity { Id = ArtistId, Name = NewName, RenamePendingFrom = OldName }];
        PlaylistGroupDto group = new() { Filters = [new PlaylistFilterDto { Entity = SmartPlaylistEntity.Artists, Field = SmartPlaylistField.Name, Operator = SmartPlaylistOperator.Contains, Value = OldName }] };
        PlaylistList = [new PlaylistHeaderEntity { Id = 5, Type = 0, GroupsJson = JsonSerializer.Serialize(new List<PlaylistGroupDto> { group }) }];

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Empty(result.Value.ChangedPlaylistIds);
        _renameRepository.Verify(r => r.ApplyAsync(It.Is<ArtistRenameDbChange>(c => c.Criteria.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "resume_does_not_move_the_picture_folder")]
    public async Task RenameAsync_Resume_DoesNotMoveThePictureFolder()
    {
        // Arrange
        Artists = [new ArtistEntity { Id = ArtistId, Name = NewName, RenamePendingFrom = OldName }];

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        _picture.Verify(p => p.MoveFolder(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _picture.Verify(p => p.CheckFolderMove(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
    }

    [Fact(DisplayName = "a_normal_rename_does_not_store_the_file_info_of_files_that_already_carry_the_new_name")]
    public async Task RenameAsync_NormalRename_DoesNotStoreFileInfoOfAlreadyRenamedFiles()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.mp3")
            ? new TagWriteCheck(path, ETagWriteReason.None, NewName) { FileDateModified = FileDate, Size = 5 }
            : new TagWriteCheck(path, ETagWriteReason.None, OldName) { FileDateModified = FileDate, Size = 7 });

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath == @"C:\music\b.mp3"), It.IsAny<CancellationToken>()), Times.Never);
        _tracks.Verify(r => r.UpdateFileInfoAsync(11, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _tracks.Verify(r => r.UpdateFileInfoAsync(10, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _tracks.Verify(r => r.UpdateFileInfoAsync(12, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "a_different_name_during_a_pending_rename_is_refused")]
    public async Task RenameAsync_DifferentNameWhilePending_IsRefused()
    {
        // Arrange
        Artists = [new ArtistEntity { Id = ArtistId, Name = NewName, RenamePendingFrom = OldName }];

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, "Another", null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.rename.pending");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "disabled_option_refuses")]
    public async Task RenameAsync_OptionDisabled_Refuses()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);
        Result<ArtistRenamePreview> preview = await _sut.PreviewAsync(ArtistId, NewName, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.rename.disabled");
        preview.Should().BeFailure().And.HaveErrorWithCode("artist.rename.disabled");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "an_unknown_artist_is_not_found")]
    public async Task RenameAsync_UnknownArtist_IsNotFound()
    {
        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(99, NewName, null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("artist.not_found");
    }

    [Fact(DisplayName = "the_same_name_is_unchanged")]
    public async Task RenameAsync_SameName_IsUnchanged()
    {
        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, $" {OldName} ", null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.rename.unchanged");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "a_case_only_rename_is_not_a_collision_and_rewrites_the_files")]
    public async Task RenameAsync_CaseOnly_RewritesTheFiles()
    {
        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, "OLD BAND", null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "rename_into_an_existing_name_is_refused_as_a_collision")]
    public async Task RenameAsync_Collision_IsRefused()
    {
        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, "OTHER band", null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<ConflictError>().And.HaveErrorWithCode("artist.rename.collision");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "rename_rewrites_the_criteria_and_reports_the_changed_playlists")]
    public async Task RenameAsync_RewritesCriteria_AndReportsChangedPlaylists()
    {
        // Arrange
        PlaylistGroupDto group = new() { Filters = [new PlaylistFilterDto { Entity = SmartPlaylistEntity.Artists, Field = SmartPlaylistField.Name, Operator = SmartPlaylistOperator.Equals, Value = OldName }] };
        PlaylistGroupDto untouched = new() { Filters = [new PlaylistFilterDto { Entity = SmartPlaylistEntity.Albums, Field = SmartPlaylistField.Name, Operator = SmartPlaylistOperator.Equals, Value = OldName }] };
        PlaylistList =
        [
            new PlaylistHeaderEntity { Id = 5, Type = 0, GroupsJson = JsonSerializer.Serialize(new List<PlaylistGroupDto> { group }) },
            new PlaylistHeaderEntity { Id = 6, Type = 0, GroupsJson = JsonSerializer.Serialize(new List<PlaylistGroupDto> { untouched }) },
            new PlaylistHeaderEntity { Id = 7, Type = 1, Picture = "old band" },
            new PlaylistHeaderEntity { Id = 8, Type = 1, Picture = "Somebody" },
            new PlaylistHeaderEntity { Id = 9, Type = 0, GroupsJson = "not json" }
        ];

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, CancellationToken.None);

        // Assert
        Assert.Equal([5L, 7L], result.Value.ChangedPlaylistIds);
        _renameRepository.Verify(r => r.ApplyAsync(
            It.Is<ArtistRenameDbChange>(c => c.Criteria.Count == 1 && c.Criteria[0].PlaylistId == 5 && c.Criteria[0].GroupsJson.Contains(NewName) && !c.Criteria[0].GroupsJson.Contains(OldName)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "cancellation_stops_between_two_files_and_lists_the_rest")]
    public async Task RenameAsync_Cancelled_ListsTheRemainingFiles()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("a.mp3")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cts.Cancel();

                return (IReadOnlyList<TagWriteResult>)[Written(@"C:\music\a.mp3", NewName)];
            });

        // Act
        Result<ArtistRenameOutcome> result = await _sut.RenameAsync(ArtistId, NewName, null, cts.Token);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(2, result.Value.Failures.Count);
        Assert.All(result.Value.Failures, f => Assert.Equal(ETagWriteReason.Cancelled, f.Reason));
    }

    private sealed class SyncProgress(Action<ArtistRenameProgress> report) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => report(value);
    }
}