using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Tag;

namespace Rok.ApplicationTests.Features.Artists.Rename;

public class ArtistFileRewriterTests
{
    private const string OldName = "Old Band";
    private const string NewName = "New Band";

    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<ITrackRepository> _tracks = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly ArtistFileRewriter _sut;

    public ArtistFileRewriterTests()
    {
        _tracks.Setup(r => r.UpdateFileInfoAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) => (IReadOnlyList<TagWriteResult>)[Written(requests[0].FilePath, requests[0].Changes.Artist!)]);
        _sut = new ArtistFileRewriter(_writer.Object, _tracks.Object, _player.Object, NullLogger<ArtistFileRewriter>.Instance);
    }

    private static TrackEntity Track(long id, string file)
    {
        return new TrackEntity { Id = id, AlbumId = 1, MusicFile = file };
    }

    private static TagWriteResult Written(string path, string artist)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = new TagFieldValues("T", 1, null, "", null) { Artist = artist } };
    }

    private void SetupChecks(Func<string, TagWriteCheck> factory)
    {
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) => (IReadOnlyList<TagWriteCheck>)paths.Select(factory).ToList());
    }

    private static TagWriteCheck Check(string path, string? artist, ETagWriteReason reason = ETagWriteReason.None)
    {
        return new TagWriteCheck(path, reason, artist) { FileDateModified = FileDate, Size = 7 };
    }

    [Fact(DisplayName = "rewriter_classifies_files_like_the_rename")]
    public async Task AnalyzeAsync_ClassifiesFiles()
    {
        // Arrange
        List<TrackEntity> tracks = [Track(1, "todo.mp3"), Track(2, "done.mp3"), Track(3, "foreign.mp3"), Track(4, "locked.mp3"), Track(5, "playing.mp3")];
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 5, MusicFile = "playing.mp3" });
        SetupChecks(path => path switch
        {
            "todo.mp3" => Check(path, OldName),
            "done.mp3" => Check(path, NewName),
            "foreign.mp3" => Check(path, "Somebody Else"),
            "locked.mp3" => Check(path, null, ETagWriteReason.ReadOnly),
            _ => Check(path, OldName)
        });

        // Act
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync(tracks, OldName, NewName, [], CancellationToken.None);

        // Assert
        Assert.Equal(2, analysis.Files.Count);
        Assert.True(analysis.Files.Single(f => f.Track.Id == 1).NeedsWrite);
        Assert.False(analysis.Files.Single(f => f.Track.Id == 2).NeedsWrite);
        Assert.Contains(analysis.Blockers, b => b.Reason == EArtistRenameBlock.OutOfSync && b.FilePath == "foreign.mp3" && b.Detail == "Somebody Else");
        Assert.Contains(analysis.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == "locked.mp3");
        Assert.Contains(analysis.Blockers, b => b.Reason == EArtistRenameBlock.Playing && b.FilePath == "playing.mp3");
        Assert.Equal(3, analysis.Blockers.Count);
    }

    [Fact(DisplayName = "rewriter_blocks_when_a_playback_only_track_is_playing_and_never_writes_it")]
    public async Task AnalyzeAsync_PlaybackOnlyTrackPlaying_Blocks()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 9, MusicFile = "other.mp3" });
        SetupChecks(path => Check(path, OldName));

        // Act
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "a.mp3")], OldName, NewName, [Track(9, "other.mp3")], CancellationToken.None);

        // Assert
        Assert.Single(analysis.Files);
        ArtistRenameBlocker blocker = Assert.Single(analysis.Blockers);
        Assert.Equal(EArtistRenameBlock.Playing, blocker.Reason);
        Assert.Equal("other.mp3", blocker.FilePath);
        _writer.Verify(w => w.CheckAsync(It.Is<IReadOnlyList<string>>(p => p.Single() == "a.mp3"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "rewriter_writes_only_the_files_to_write_and_reports_progress")]
    public async Task WriteAsync_WritesOnlyFilesToWrite()
    {
        // Arrange
        SetupChecks(path => path == "done.mp3" ? Check(path, NewName) : Check(path, OldName));
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "a.mp3"), Track(2, "done.mp3"), Track(3, "b.mp3")], OldName, NewName, [], CancellationToken.None);
        List<ArtistRenameProgress> reports = [];

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(analysis, NewName, false, new SyncProgress(reports.Add), CancellationToken.None);

        // Assert
        Assert.Empty(failures);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _tracks.Verify(r => r.UpdateFileInfoAsync(2, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        Assert.Equal([new ArtistRenameProgress(1, 2), new ArtistRenameProgress(2, 2)], reports);
    }

    [Fact(DisplayName = "rewriter_stores_the_file_info_of_done_files_only_when_asked")]
    public async Task WriteAsync_StoreInfoOfDoneFiles_OnlyWhenAsked()
    {
        // Arrange
        SetupChecks(path => Check(path, NewName));
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync([Track(2, "done.mp3")], OldName, NewName, [], CancellationToken.None);

        // Act
        await _sut.WriteAsync(analysis, NewName, false, null, CancellationToken.None);
        await _sut.WriteAsync(analysis, NewName, true, null, CancellationToken.None);

        // Assert
        _tracks.Verify(r => r.UpdateFileInfoAsync(2, FileDate.DateTime, 7, It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "rewriter_lists_a_failing_file_and_goes_on_with_the_next")]
    public async Task WriteAsync_FailingFile_IsListedAndLoopContinues()
    {
        // Arrange
        SetupChecks(path => Check(path, OldName));
        _writer.Setup(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(r => r[0].FilePath == "a.mp3"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult("a.mp3", ETagWriteOutcome.Skipped, ETagWriteReason.Locked, null, null)]);
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "a.mp3"), Track(2, "b.mp3")], OldName, NewName, [], CancellationToken.None);

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(analysis, NewName, false, null, CancellationToken.None);

        // Assert
        Assert.Equal(new ArtistRenameFailure("a.mp3", ETagWriteReason.Locked), Assert.Single(failures));
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _tracks.Verify(r => r.UpdateFileInfoAsync(1, It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "rewriter_lists_the_remaining_files_as_cancelled")]
    public async Task WriteAsync_Cancelled_ListsRemainingFiles()
    {
        // Arrange
        SetupChecks(path => Check(path, OldName));
        ArtistFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "a.mp3")], OldName, NewName, [], CancellationToken.None);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(analysis, NewName, false, null, cts.Token);

        // Assert
        Assert.Equal(ETagWriteReason.Cancelled, Assert.Single(failures).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class SyncProgress(Action<ArtistRenameProgress> report) : IProgress<ArtistRenameProgress>
    {
        public void Report(ArtistRenameProgress value)
        {
            report(value);
        }
    }
}