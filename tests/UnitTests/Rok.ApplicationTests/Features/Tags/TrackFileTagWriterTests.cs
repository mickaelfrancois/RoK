using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Tags;
using Rok.Application.Player;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Tags;

public class TrackFileTagWriterTests
{
    private static readonly DateTimeOffset FileDate = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IPlayerService> _player = new();
    private readonly List<(long Id, DateTimeOffset? Date, long? Size)> _written = [];
    private readonly TrackFileTagWriter _sut;

    public TrackFileTagWriterTests()
    {
        _sut = new TrackFileTagWriter(_writer.Object, _player.Object, NullLogger<TrackFileTagWriter>.Instance);
    }

    private static TrackEntity Track(long id, string file)
    {
        return new TrackEntity { Id = id, AlbumId = 1, MusicFile = file };
    }

    private static TagWriteCheck Check(string path, ETagWriteReason reason = ETagWriteReason.None)
    {
        return new TagWriteCheck(path, reason, "Artist") { FileDateModified = FileDate, Size = 7 };
    }

    private static TagWriteResult Succeeded(string path, TagFieldValues? values)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, FileDate, 100) { Values = values };
    }

    private static TagFieldValues Values(string artist = "", string album = "", string genre = "", int? year = null)
    {
        return new TagFieldValues("T", 1, null, "", null) { Artist = artist, Album = album, Genre = genre, Year = year };
    }

    private void SetupChecks(Func<string, TagWriteCheck> factory)
    {
        _writer.Setup(w => w.CheckAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> paths, CancellationToken _) => (IReadOnlyList<TagWriteCheck>)paths.Select(factory).ToList());
    }

    private void SetupWrite(Func<TagWriteRequest, TagWriteResult> factory)
    {
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TagWriteRequest> requests, CancellationToken _) => (IReadOnlyList<TagWriteResult>)[factory(requests[0])]);
    }

    private Task OnWrittenAsync(long id, DateTimeOffset? date, long? size)
    {
        _written.Add((id, date, size));

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "track_file_tag_writer_lists_playing_and_unwritable_files_as_blockers")]
    public async Task AnalyzeAsync_PlayingAndUnwritableFiles_AreBlockers()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 3, MusicFile = "playing.mp3" });
        SetupChecks(path => path == "locked.mp3" ? Check(path, ETagWriteReason.ReadOnly) : Check(path));

        // Act
        TrackFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "ok.mp3"), Track(2, "locked.mp3"), Track(3, "playing.mp3")], [], CancellationToken.None);

        // Assert
        Assert.Equal("ok.mp3", Assert.Single(analysis.Files).Track.MusicFile);
        Assert.Contains(analysis.Blockers, b => b.Reason == EArtistRenameBlock.ReadOnly && b.FilePath == "locked.mp3");
        Assert.Contains(analysis.Blockers, b => b.Reason == EArtistRenameBlock.Playing && b.FilePath == "playing.mp3");
        Assert.Equal(2, analysis.Blockers.Count);
    }

    [Fact(DisplayName = "track_file_tag_writer_blocks_when_a_playback_only_track_is_playing_and_never_checks_it")]
    public async Task AnalyzeAsync_PlaybackOnlyTrackPlaying_BlocksWithoutChecking()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 9, MusicFile = "other.mp3" });
        SetupChecks(path => Check(path));

        // Act
        TrackFileAnalysis analysis = await _sut.AnalyzeAsync([Track(1, "a.mp3")], [Track(9, "other.mp3")], CancellationToken.None);

        // Assert
        Assert.Single(analysis.Files);
        Assert.Equal(EArtistRenameBlock.Playing, Assert.Single(analysis.Blockers).Reason);
        _writer.Verify(w => w.CheckAsync(It.Is<IReadOnlyList<string>>(p => p.Single() == "a.mp3"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "track_file_tag_writer_turns_a_classified_file_into_a_blocker")]
    public async Task AnalyzeAsync_ClassifierBlocker_IsListed()
    {
        // Arrange
        SetupChecks(path => Check(path));

        // Act
        TrackFileAnalysis analysis = await _sut.AnalyzeAsync(
            [Track(1, "a.mp3"), Track(2, "b.mp3")],
            [],
            CancellationToken.None,
            (track, _) => track.Id == 2 ? new ArtistRenameBlocker(track.MusicFile, EArtistRenameBlock.OutOfSync, "x") : null);

        // Assert
        Assert.Equal(1, Assert.Single(analysis.Files).Track.Id);
        Assert.Equal(EArtistRenameBlock.OutOfSync, Assert.Single(analysis.Blockers).Reason);
    }

    [Fact(DisplayName = "track_file_tag_writer_writes_each_file_reports_progress_and_calls_on_written")]
    public async Task WriteAsync_WritesEachFile_ReportsProgressAndCallsOnWrittenAsync()
    {
        // Arrange
        SetupWrite(r => Succeeded(r.FilePath, Values(album: r.Changes.Album!)));
        List<ArtistRenameProgress> reports = [];
        PlannedFileWrite[] writes = [new(Track(1, "a.mp3"), new TagChanges { Album = "New" }), new(Track(2, "b.mp3"), new TagChanges { Album = "New" })];

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(writes, OnWrittenAsync, new SyncProgress(reports.Add), CancellationToken.None);

        // Assert
        Assert.Empty(failures);
        Assert.Equal([new ArtistRenameProgress(1, 2), new ArtistRenameProgress(2, 2)], reports);
        Assert.Equal([(1L, (DateTimeOffset?)FileDate, (long?)100), (2L, (DateTimeOffset?)FileDate, (long?)100)], _written);
    }

    [Theory(DisplayName = "track_file_tag_writer_verifies_every_requested_field")]
    [InlineData("artist")]
    [InlineData("album")]
    [InlineData("genre")]
    [InlineData("year")]
    public async Task WriteAsync_WrongReadBack_IsVerificationFailure(string field)
    {
        // Arrange
        TagChanges changes = field switch
        {
            "artist" => new TagChanges { Artist = "New" },
            "album" => new TagChanges { Album = "New" },
            "genre" => new TagChanges { Genre = "New" },
            _ => new TagChanges { Year = 2001 }
        };
        SetupWrite(r => Succeeded(r.FilePath, Values(artist: "Old", album: "Old", genre: "Old", year: 1999)));

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync([new PlannedFileWrite(Track(1, "a.mp3"), changes)], OnWrittenAsync, null, CancellationToken.None);

        // Assert
        Assert.Equal(new ArtistRenameFailure("a.mp3", ETagWriteReason.VerificationFailed), Assert.Single(failures));
        Assert.Empty(_written);
    }

    [Fact(DisplayName = "track_file_tag_writer_fails_verification_when_a_requested_field_comes_back_without_values")]
    public async Task WriteAsync_NoValues_IsVerificationFailure()
    {
        // Arrange
        SetupWrite(r => Succeeded(r.FilePath, null));

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync([new PlannedFileWrite(Track(1, "a.mp3"), new TagChanges { Genre = "New" })], OnWrittenAsync, null, CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.VerificationFailed, Assert.Single(failures).Reason);
    }

    [Fact(DisplayName = "track_file_tag_writer_accepts_a_field_the_write_result_does_not_carry")]
    public async Task WriteAsync_DiscOnly_IsAcceptedWithoutValues()
    {
        // Arrange
        SetupWrite(r => Succeeded(r.FilePath, null));

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync([new PlannedFileWrite(Track(1, "a.mp3"), new TagChanges { Disc = 2 })], OnWrittenAsync, null, CancellationToken.None);

        // Assert
        Assert.Empty(failures);
        Assert.Single(_written);
    }

    [Fact(DisplayName = "track_file_tag_writer_lists_a_failing_file_and_goes_on_with_the_next")]
    public async Task WriteAsync_FailingFile_IsListedAndLoopContinues()
    {
        // Arrange
        SetupWrite(r => r.FilePath == "a.mp3"
            ? new TagWriteResult("a.mp3", ETagWriteOutcome.Skipped, ETagWriteReason.Locked, null, null)
            : Succeeded(r.FilePath, Values(album: "New")));
        PlannedFileWrite[] writes = [new(Track(1, "a.mp3"), new TagChanges { Album = "New" }), new(Track(2, "b.mp3"), new TagChanges { Album = "New" })];

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(writes, OnWrittenAsync, null, CancellationToken.None);

        // Assert
        Assert.Equal(new ArtistRenameFailure("a.mp3", ETagWriteReason.Locked), Assert.Single(failures));
        Assert.Equal(2, Assert.Single(_written).Id);
    }

    [Fact(DisplayName = "track_file_tag_writer_turns_a_writer_exception_into_an_io_failure")]
    public async Task WriteAsync_WriterThrows_IsIoError()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("boom"));

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync([new PlannedFileWrite(Track(1, "a.mp3"), new TagChanges { Album = "New" })], OnWrittenAsync, null, CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.IoError, Assert.Single(failures).Reason);
    }

    [Fact(DisplayName = "track_file_tag_writer_keeps_a_written_file_successful_when_on_written_throws")]
    public async Task WriteAsync_OnWrittenAsyncThrows_FileStaysSuccessful()
    {
        // Arrange
        SetupWrite(r => Succeeded(r.FilePath, Values(album: "New")));

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync(
            [new PlannedFileWrite(Track(1, "a.mp3"), new TagChanges { Album = "New" })],
            (_, _, _) => throw new InvalidOperationException("db"),
            null,
            CancellationToken.None);

        // Assert
        Assert.Empty(failures);
    }

    [Fact(DisplayName = "track_file_tag_writer_lists_the_remaining_files_as_cancelled")]
    public async Task WriteAsync_Cancelled_ListsRemainingFiles()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        List<ArtistRenameFailure> failures = await _sut.WriteAsync([new PlannedFileWrite(Track(1, "a.mp3"), new TagChanges { Album = "New" })], OnWrittenAsync, null, cts.Token);

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