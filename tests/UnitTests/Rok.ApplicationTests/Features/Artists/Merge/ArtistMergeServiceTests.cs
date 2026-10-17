using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Enums;
using Rok.Domain.Interfaces.Entities;
using Rok.Shared.Enums;

namespace Rok.ApplicationTests.Features.Artists.Merge;

public class ArtistMergeServiceTests
{
    private const long SourceId = 1;
    private const long TargetId = 2;
    private const string SourceName = "Beatles";
    private const string TargetName = "The Beatles";

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<IArtistRepository> _artists = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IAlbumRepository> _albums = new();
    private readonly Mock<IPlaylistHeaderRepository> _playlists = new();
    private readonly Mock<IArtistMergeRepository> _mergeRepository = new();
    private readonly Mock<IArtistRenameRepository> _renameRepository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IArtistPicture> _picture = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly List<string> _calls = [];
    private readonly List<TrackEntity> _sourceTracks;
    private readonly ArtistMergeService _sut;

    public ArtistMergeServiceTests()
    {
        _sourceTracks =
        [
            new TrackEntity { Id = 10, AlbumId = 100, ArtistId = SourceId, MusicFile = @"C:\music\a.mp3" },
            new TrackEntity { Id = 11, AlbumId = 100, ArtistId = SourceId, MusicFile = @"C:\music\b.mp3" },
            new TrackEntity { Id = 12, AlbumId = 101, ArtistId = SourceId, MusicFile = @"C:\music\c.flac" }
        ];

        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _artists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => Artists);
        _tracks.Setup(r => r.GetByArtistIdAsync(SourceId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => _sourceTracks);
        _tracks.Setup(r => r.GetByArtistIdAsync(TargetId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => TargetTracks);
        _tracks.Setup(r => r.UpdateFileInfoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<long, DateTime, long, RepositoryConnectionKind>((id, _, _, _) => _calls.Add($"info:{id}"))
            .ReturnsAsync(true);
        _albums.Setup(r => r.GetByArtistIdAsync(SourceId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => (IEnumerable<IAlbumEntity>)SourceAlbums);
        _albums.Setup(r => r.GetByArtistIdAsync(TargetId, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => (IEnumerable<IAlbumEntity>)TargetAlbums);
        _playlists.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(() => PlaylistList);
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((_, _) => _calls.Add("apply"))
            .Returns(Task.CompletedTask);
        _renameRepository.Setup(r => r.CompleteAsync(TargetId, It.IsAny<CancellationToken>()))
            .Callback<long, CancellationToken>((_, _) => _calls.Add("complete"))
            .ReturnsAsync(true);
        _picture.Setup(p => p.MergeFolderInto(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Callback<string, string, IReadOnlyCollection<string>>((from, to, _) => _calls.Add($"folder:{from}>{to}"))
            .Returns(ArtistFolderMergeOutcome.Merged);
        SetupChecks(path => new TagWriteCheck(path, ETagWriteReason.None, SourceName) { FileDateModified = FileDate, Size = 99 });
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) =>
            {
                _calls.Add($"write:{requests[0].FilePath}");

                return (IReadOnlyList<TagWriteResult>)[Written(requests[0].FilePath, requests[0].Changes.Artist!)];
            });

        _sut = new ArtistMergeService(
            _artists.Object,
            _tracks.Object,
            _albums.Object,
            _playlists.Object,
            _mergeRepository.Object,
            _renameRepository.Object,
            new ArtistFileRewriter(_writer.Object, _tracks.Object, _player.Object, NullLogger<ArtistFileRewriter>.Instance),
            _picture.Object,
            _import.Object,
            _gate,
            _options.Object,
            NullLogger<ArtistMergeService>.Instance);
    }

    private List<ArtistEntity> Artists { get; set; } =
    [
        new ArtistEntity { Id = SourceId, Name = SourceName },
        new ArtistEntity { Id = TargetId, Name = TargetName },
        new ArtistEntity { Id = 3, Name = "Other Band" }
    ];

    private List<TrackEntity> TargetTracks { get; set; } = [new TrackEntity { Id = 20, AlbumId = 200, ArtistId = TargetId, MusicFile = @"C:\music\target.mp3" }];

    private List<AlbumEntity> SourceAlbums { get; set; } =
    [
        new AlbumEntity { Id = 100, Name = "Abbey Road", ArtistId = SourceId },
        new AlbumEntity { Id = 101, Name = "Help", ArtistId = SourceId }
    ];

    private List<AlbumEntity> TargetAlbums { get; set; } =
    [
        new AlbumEntity { Id = 200, Name = "abbey road", ArtistId = TargetId }
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

    private static PlaylistHeaderEntity SmartPlaylist(long id, string value)
    {
        List<PlaylistGroupDto> groups =
        [
            new PlaylistGroupDto
            {
                Filters = [new PlaylistFilterDto { Entity = SmartPlaylistEntity.Artists, Field = SmartPlaylistField.Name, Operator = SmartPlaylistOperator.Equals, Value = value }]
            }
        ];

        return new PlaylistHeaderEntity { Id = id, Name = $"Smart {id}", Type = 0, GroupsJson = JsonSerializer.Serialize(groups) };
    }

    private void VerifyNothingChanged()
    {
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _mergeRepository.Verify(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()), Times.Never);
        _picture.Verify(p => p.MergeFolderInto(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "preview_refuses_when_tags_writing_is_off")]
    public async Task PreviewAsync_OptionDisabled_Fails()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        Result<ArtistMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.merge.disabled");
    }

    [Theory(DisplayName = "preview_refuses_when_either_artist_has_a_pending_rename")]
    [InlineData(SourceId)]
    [InlineData(TargetId)]
    public async Task PreviewAsync_PendingRename_Fails(long pendingId)
    {
        // Arrange
        Artists.Single(a => a.Id == pendingId).RenamePendingFrom = "Former";

        // Act
        Result<ArtistMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.merge.pending_rename");
    }

    [Fact(DisplayName = "preview_refuses_when_ids_are_equal")]
    public async Task PreviewAsync_SameArtist_Fails()
    {
        // Act
        Result<ArtistMergePreview> result = await _sut.PreviewAsync(SourceId, SourceId, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.merge.same_artist");
    }

    [Theory(DisplayName = "preview_returns_not_found_for_an_unknown_artist")]
    [InlineData(99, TargetId)]
    [InlineData(SourceId, 99)]
    public async Task PreviewAsync_UnknownArtist_ReturnsNotFound(long sourceId, long targetId)
    {
        // Act
        Result<ArtistMergePreview> result = await _sut.PreviewAsync(sourceId, targetId, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveError<NotFoundError>().And.HaveErrorWithCode("artist.not_found");
    }

    [Fact(DisplayName = "preview_lists_blockers_files_albums_conflicts_and_playlists")]
    public async Task PreviewAsync_ListsEverything()
    {
        // Arrange
        Artists.Single(a => a.Id == SourceId).Biography = "Source biography";
        Artists.Single(a => a.Id == TargetId).Biography = "Target biography";
        PlaylistList = [SmartPlaylist(7, SourceName), SmartPlaylist(8, "Unrelated")];
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 20, MusicFile = @"C:\music\target.mp3" });
        _picture.Setup(p => p.PictureFileExists(SourceName)).Returns(true);
        SetupChecks(path => path.EndsWith("b.mp3")
            ? new TagWriteCheck(path, ETagWriteReason.ReadOnly, null)
            : new TagWriteCheck(path, ETagWriteReason.None, SourceName) { FileDateModified = FileDate, Size = 1 });

        // Act
        Result<ArtistMergePreview> result = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        ArtistMergePreview preview = result.Value;
        Assert.Equal(3, preview.FileCount);
        Assert.Equal(2, preview.AlbumCount);
        Assert.Equal(SourceName, preview.SourceName);
        Assert.Equal(TargetName, preview.TargetName);
        Assert.Equal(new AlbumMergeGroupPreview("abbey road", 2), Assert.Single(preview.MergedAlbums));
        Assert.Equal(nameof(ArtistEntity.Biography), Assert.Single(preview.Conflicts).Field);
        Assert.Equal(7, Assert.Single(preview.Playlists).PlaylistId);
        Assert.Equal(EArtistMergePicture.Source, preview.Picture);
        Assert.Contains(preview.Blockers, b => b.Reason == EArtistRenameBlock.Playing && b.FilePath == @"C:\music\target.mp3");
        Assert.Contains(preview.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == @"C:\music\b.mp3");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_with_a_blocker_changes_nothing")]
    public async Task MergeAsync_WithBlocker_ChangesNothing()
    {
        // Arrange
        SetupChecks(path => path.EndsWith("b.mp3")
            ? new TagWriteCheck(path, ETagWriteReason.Locked, null)
            : new TagWriteCheck(path, ETagWriteReason.None, SourceName) { FileDateModified = FileDate, Size = 1 });

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.Locked, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_rechecks_under_the_gate")]
    public async Task MergeAsync_RechecksUnderTheGate()
    {
        // Arrange
        Result<ArtistMergePreview> preview = await _sut.PreviewAsync(SourceId, TargetId, CancellationToken.None);
        Assert.Empty(preview.Value.Blockers);
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 20, MusicFile = @"C:\music\target.mp3" });

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.Playing, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_during_import_is_refused")]
    public async Task MergeAsync_ImportInProgress_IsBlocked()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Blocked, result.Value.State);
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_when_the_gate_is_held_is_refused")]
    public async Task MergeAsync_GateHeld_IsBlocked()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));

        // Act
        Result<ArtistMergeOutcome> result;

        using (lease)
            result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameBlock.ImportInProgress, Assert.Single(result.Value.Blockers).Reason);
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "the_gate_is_released_after_the_merge")]
    public async Task MergeAsync_ReleasesTheGate()
    {
        // Act
        await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }

    [Fact(DisplayName = "merge_refuses_before_any_check_when_the_ids_are_equal")]
    public async Task MergeAsync_SameArtist_Fails()
    {
        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, SourceId, null, CancellationToken.None);

        // Assert
        result.Should().BeFailure().And.HaveErrorWithCode("artist.merge.same_artist");
        VerifyNothingChanged();
    }

    [Fact(DisplayName = "merge_applies_db_then_folder_then_files_then_clears_the_marker")]
    public async Task MergeAsync_AppliesDbThenFolderThenFiles_AndClearsTheMarker()
    {
        // Arrange
        List<ArtistRenameProgress> reports = [];
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) =>
            {
                change = c;
                _calls.Add("apply");
            })
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, new SyncProgress(reports.Add), CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        ArtistMergeOutcome outcome = result.Value;
        Assert.Equal(EArtistRenameState.Completed, outcome.State);
        Assert.Equal(TargetId, outcome.ArtistId);
        Assert.Equal(TargetName, outcome.Name);
        Assert.Equal(SourceId, outcome.DeletedArtistId);
        Assert.Equal(
            [
                "apply",
                $"folder:{SourceName}>{TargetName}",
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
        Assert.NotNull(change);
        Assert.Equal(SourceId, change.SourceId);
        Assert.Equal(TargetId, change.TargetId);
        Assert.Equal(SourceName, change.SourceName);
        Assert.Equal(TargetName, change.TargetName);
        Assert.Equal(SourceName, change.PendingFrom);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r.Count == 1 && r[0].Changes == new TagChanges { Artist = TargetName }), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "merge_reports_the_albums_the_playlists_and_the_rewritten_criteria")]
    public async Task MergeAsync_ReportsAlbumsAndPlaylists()
    {
        // Arrange
        PlaylistList =
        [
            SmartPlaylist(7, SourceName),
            SmartPlaylist(8, "Unrelated"),
            new PlaylistHeaderEntity { Id = 9, Name = "Manual", Type = 1, Picture = SourceName.ToLowerInvariant() }
        ];
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) => change = c)
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        ArtistMergeOutcome outcome = result.Value;
        Assert.Equal([7L, 9L], outcome.ChangedPlaylistIds);
        Assert.Equal([100L], outcome.DeletedAlbumIds);
        Assert.Equal([101L, 200L], outcome.UpdatedAlbumIds.Order());
        Assert.NotNull(change);
        PlaylistCriteriaChange criteria = Assert.Single(change.Criteria);
        Assert.Equal(7, criteria.PlaylistId);
        Assert.Contains(TargetName, criteria.GroupsJson);
        AlbumMergeGroup group = Assert.Single(change.AlbumGroups);
        Assert.Equal(200, group.SurvivorId);
        Assert.Equal([100L], group.MergedIds);
    }

    [Fact(DisplayName = "merge_without_file_to_write_sets_no_marker")]
    public async Task MergeAsync_NoFileToWrite_SetsNoMarker()
    {
        // Arrange
        SetupChecks(path => new TagWriteCheck(path, ETagWriteReason.None, TargetName) { FileDateModified = FileDate, Size = 99 });
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) => change = c)
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.NotNull(change);
        Assert.Null(change.PendingFrom);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "a_folder_merge_failure_does_not_fail_the_merge")]
    public async Task MergeAsync_FolderMergeFails_StillCompletes()
    {
        // Arrange
        _picture.Setup(p => p.MergeFolderInto(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>())).Throws(new IOException("locked"));

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Completed, result.Value.State);
        Assert.Empty(result.Value.Failures);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _renameRepository.Verify(r => r.CompleteAsync(TargetId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "the_folder_merge_receives_the_names_of_the_other_living_artists")]
    public async Task MergeAsync_FolderMerge_ReceivesTheOtherNames()
    {
        // Act
        await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        _picture.Verify(p => p.MergeFolderInto(SourceName, TargetName, It.Is<IReadOnlyCollection<string>>(names => names.Single() == "Other Band")), Times.Once);
    }

    [Fact(DisplayName = "a_write_incident_keeps_the_marker_and_lists_the_file")]
    public async Task MergeAsync_WriteIncident_ReturnsPartial()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath.EndsWith("b.mp3")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Skipped(@"C:\music\b.mp3", ETagWriteReason.Locked)]);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Equal(new ArtistRenameFailure(@"C:\music\b.mp3", ETagWriteReason.Locked), Assert.Single(result.Value.Failures));
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "a_marker_that_cannot_be_cleared_gives_a_partial_merge")]
    public async Task MergeAsync_MarkerNotCleared_ReturnsPartial()
    {
        // Arrange
        _renameRepository.Setup(r => r.CompleteAsync(TargetId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Partial, result.Value.State);
        Assert.Empty(result.Value.Failures);
    }

    [Fact(DisplayName = "a_database_failure_changes_nothing_else")]
    public async Task MergeAsync_DatabaseFails_ReturnsFailedWithoutFolderOrFiles()
    {
        // Arrange
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        Assert.Equal(EArtistRenameState.Failed, result.Value.State);
        Assert.Equal(0, result.Value.DeletedArtistId);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _picture.Verify(p => p.MergeFolderInto(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        _renameRepository.Verify(r => r.CompleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class SyncProgress(Action<ArtistRenameProgress> report) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value) => report(value);
    }

    [Fact(DisplayName = "merge_converts_a_compilation_whose_tracks_all_belong_to_the_two_artists")]
    public async Task MergeAsync_CompilationOfTheTwoArtistsOnly_IsConverted()
    {
        // Arrange
        SourceAlbums.Add(new AlbumEntity { Id = 102, Name = "Same Folder", ArtistId = SourceId, IsCompilation = true });
        _tracks.Setup(r => r.GetByAlbumIdAsync(102L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(
        [
            new TrackEntity { Id = 30, AlbumId = 102, ArtistId = SourceId },
            new TrackEntity { Id = 31, AlbumId = 102, ArtistId = TargetId }
        ]);
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) => change = c)
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Equal([102L], change!.ConvertedCompilationIds);
        Assert.Contains(102L, result.Value.UpdatedAlbumIds);
    }

    [Theory(DisplayName = "merge_leaves_a_tag_or_manual_compilation_untouched")]
    [InlineData(ECompilationSource.Tag)]
    [InlineData(ECompilationSource.Manual)]
    public async Task MergeAsync_NonDerivedCompilation_IsNotConverted(ECompilationSource source)
    {
        // Arrange
        SourceAlbums.Add(new AlbumEntity { Id = 102, Name = "Same Folder", ArtistId = SourceId, IsCompilation = true, CompilationSource = source });
        _tracks.Setup(r => r.GetByAlbumIdAsync(102L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(
        [
            new TrackEntity { Id = 30, AlbumId = 102, ArtistId = SourceId },
            new TrackEntity { Id = 31, AlbumId = 102, ArtistId = TargetId }
        ]);
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) => change = c)
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Empty(change!.ConvertedCompilationIds!);
    }

    [Fact(DisplayName = "merge_leaves_a_compilation_with_a_third_artist_untouched")]
    public async Task MergeAsync_CompilationWithAThirdArtist_IsNotConverted()
    {
        // Arrange
        SourceAlbums.Add(new AlbumEntity { Id = 102, Name = "Same Folder", ArtistId = SourceId, IsCompilation = true });
        _tracks.Setup(r => r.GetByAlbumIdAsync(102L, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(
        [
            new TrackEntity { Id = 30, AlbumId = 102, ArtistId = SourceId },
            new TrackEntity { Id = 31, AlbumId = 102, ArtistId = 3 }
        ]);
        ArtistMergeDbChange? change = null;
        _mergeRepository.Setup(r => r.ApplyAsync(It.IsAny<ArtistMergeDbChange>(), It.IsAny<CancellationToken>()))
            .Callback<ArtistMergeDbChange, CancellationToken>((c, _) => change = c)
            .Returns(Task.CompletedTask);

        // Act
        Result<ArtistMergeOutcome> result = await _sut.MergeAsync(SourceId, TargetId, null, CancellationToken.None);

        // Assert
        result.Should().BeSuccess();
        Assert.Empty(change!.ConvertedCompilationIds!);
    }
}