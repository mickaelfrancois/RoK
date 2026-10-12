using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Player;
using Rok.Application.Services;
using Rok.Application.Tag;
using Rok.Domain.Entities;

namespace Rok.ApplicationTests.Features.Tracks.Services;

public class TrackTagEditServiceTests
{
    private const string MusicFile = @"C:\music\song.mp3";

    private readonly Mock<ITrackRepository> _repository = new();
    private readonly Mock<ITagWriter> _writer = new();
    private readonly Mock<IPlayerService> _player = new(MockBehavior.Strict);
    private readonly Mock<IImport> _import = new();
    private readonly Mock<IAppOptions> _options = new();
    private readonly LibraryWriteGate _gate = new();
    private readonly TrackTagEditService _sut;

    public TrackTagEditServiceTests()
    {
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(true);
        _player.SetupGet(p => p.CurrentTrack).Returns((TrackDto?)null);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync((long id, RepositoryConnectionKind _) => new TrackEntity { Id = id, MusicFile = MusicFile });
        _repository.Setup(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(true);

        _sut = new TrackTagEditService(_repository.Object, _writer.Object, _player.Object, _import.Object, _gate, _options.Object, NullLogger<TrackTagEditService>.Instance);
    }

    private static TagWriteResult Succeeded(string path, DateTimeOffset date, long size)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, date, size);
    }

    private static TagWriteResult Skipped(string path, ETagWriteReason reason)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Skipped, reason, null, null);
    }

    [Fact(DisplayName = "when_option_is_disabled_nothing_is_read_or_written")]
    public async Task UpdateTitlesAsync_OptionDisabled_SkipsEverything()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(TrackTagEditResult.DisabledCode, result.ErrorCode);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        _repository.Verify(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "when_track_is_missing_it_is_skipped_as_not_found")]
    public async Task UpdateTitlesAsync_TrackMissing_SkipsAsNotFound()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.NotFound, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_track_is_playing_by_id_it_is_skipped_and_the_player_is_only_read")]
    public async Task UpdateTitlesAsync_TrackPlayingById_SkipsAsPlaying()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 1, MusicFile = @"C:\other.mp3" });

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.Playing, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _player.VerifyGet(p => p.CurrentTrack, Times.Once);
        _player.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "when_another_track_plays_the_same_file_with_other_casing_it_is_skipped_as_playing")]
    public async Task UpdateTitlesAsync_SameFileOtherCasing_SkipsAsPlaying()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 99, MusicFile = MusicFile.ToUpperInvariant() });

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.Playing, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_import_is_running_the_batch_is_skipped_and_the_database_is_untouched")]
    public async Task UpdateTitlesAsync_ImportInProgress_SkipsAsImportInProgress()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.ImportInProgress, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_gate_is_already_held_the_batch_is_skipped_as_import_in_progress")]
    public async Task UpdateTitlesAsync_GateHeld_SkipsAsImportInProgress()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        using IDisposable heldLease = lease;

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.ImportInProgress, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_write_succeeds_the_database_receives_title_file_date_and_size_of_the_result")]
    public async Task UpdateTitlesAsync_WriteSucceeds_UpdatesDatabase()
    {
        // Arrange
        DateTimeOffset fileDate = new(2025, 3, 4, 5, 6, 7, TimeSpan.Zero);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, fileDate, 4242)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.True(Assert.Single(results).Succeeded);
        _repository.Verify(r => r.UpdateTitleAndFileInfoAsync(1, "New", fileDate.DateTime, 4242, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(l => l.Count == 1 && l[0].FilePath == MusicFile && l[0].Changes.Title == "New"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "when_the_writer_skips_a_file_the_database_is_not_updated")]
    public async Task UpdateTitlesAsync_WriterSkips_DoesNotUpdateDatabase()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Skipped(MusicFile, ETagWriteReason.Locked)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteReason.Locked, result.Reason);
        Assert.Equal("track.tag_write.locked", result.ErrorCode);
        _repository.Verify(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_database_update_fails_after_a_successful_write_the_result_is_failed")]
    public async Task UpdateTitlesAsync_DatabaseFails_ReturnsFailed()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);
        _repository.Setup(r => r.UpdateTitleAndFileInfoAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.IoError, result.Reason);
    }

    [Fact(DisplayName = "when_a_batch_mixes_cases_results_keep_the_input_order")]
    public async Task UpdateTitlesAsync_MixedBatch_KeepsOrder()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);
        _repository.Setup(r => r.GetByIdAsync(3, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 3, MusicFile = @"C:\music\other.mp3" });
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10), Skipped(@"C:\music\other.mp3", ETagWriteReason.ReadOnly)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateTitlesAsync([(1, "A"), (2, "B"), (3, "C")], CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L, 3L], results.Select(r => r.TrackId));
        Assert.Equal(ETagWriteOutcome.Succeeded, results[0].Outcome);
        Assert.Equal(ETagWriteReason.NotFound, results[1].Reason);
        Assert.Equal(ETagWriteReason.ReadOnly, results[2].Reason);
    }

    [Fact(DisplayName = "after_the_batch_the_gate_can_be_entered_again")]
    public async Task UpdateTitlesAsync_AfterBatch_ReleasesGate()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);

        // Act
        await _sut.UpdateTitlesAsync([(1, "New")], CancellationToken.None);

        // Assert
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }
}