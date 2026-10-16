using Microsoft.Extensions.Logging;
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
        _repository.Setup(r => r.UpdateTagFieldsAndFileInfoAsync(It.IsAny<long>(), It.IsAny<TagFieldValues>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(true);

        _sut = new TrackTagEditService(_repository.Object, _writer.Object, _player.Object, _import.Object, _gate, _options.Object, NullLogger<TrackTagEditService>.Instance);
    }

    private static TagWriteResult Succeeded(string path, DateTimeOffset date, long size, TagFieldValues? values = null)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Succeeded, ETagWriteReason.None, date, size) { Values = values ?? new TagFieldValues("New", 1, null, "", null) };
    }

    private static TagWriteResult Skipped(string path, ETagWriteReason reason)
    {
        return new TagWriteResult(path, ETagWriteOutcome.Skipped, reason, null, null);
    }

    private void VerifyDatabaseNeverUpdated()
    {
        _repository.Verify(r => r.UpdateTagFieldsAndFileInfoAsync(It.IsAny<long>(), It.IsAny<TagFieldValues>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "when_option_is_disabled_nothing_is_read_or_written")]
    public async Task UpdateAsync_OptionDisabled_SkipsEverything()
    {
        // Arrange
        _options.SetupGet(o => o.WriteTagsEnabled).Returns(false);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(TrackTagEditResult.DisabledCode, result.ErrorCode);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        VerifyDatabaseNeverUpdated();
    }

    [Fact(DisplayName = "when_track_is_missing_it_is_skipped_as_not_found")]
    public async Task UpdateAsync_TrackMissing_SkipsAsNotFound()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.NotFound, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_track_is_playing_by_id_it_is_skipped_and_the_player_is_only_read")]
    public async Task UpdateAsync_TrackPlayingById_SkipsAsPlaying()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 1, MusicFile = @"C:\other.mp3" });

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.Playing, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _player.VerifyGet(p => p.CurrentTrack, Times.Once);
        _player.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "when_another_track_plays_the_same_file_with_other_casing_it_is_skipped_as_playing")]
    public async Task UpdateAsync_SameFileOtherCasing_SkipsAsPlaying()
    {
        // Arrange
        _player.SetupGet(p => p.CurrentTrack).Returns(new TrackDto { Id = 99, MusicFile = MusicFile.ToUpperInvariant() });

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.Playing, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_import_is_running_the_batch_is_skipped_and_the_database_is_untouched")]
    public async Task UpdateAsync_ImportInProgress_SkipsAsImportInProgress()
    {
        // Arrange
        _import.SetupGet(i => i.UpdateInProgress).Returns(true);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.ImportInProgress, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyDatabaseNeverUpdated();
    }

    [Fact(DisplayName = "when_the_gate_is_already_held_the_batch_is_skipped_as_import_in_progress")]
    public async Task UpdateAsync_GateHeld_SkipsAsImportInProgress()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        using IDisposable heldLease = lease;

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteReason.ImportInProgress, Assert.Single(results).Reason);
        _writer.Verify(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_write_succeeds_the_database_receives_title_file_date_and_size_of_the_result")]
    public async Task UpdateAsync_WriteSucceeds_UpdatesDatabase()
    {
        // Arrange
        DateTimeOffset fileDate = new(2025, 3, 4, 5, 6, 7, TimeSpan.Zero);
        TagFieldValues values = new("New", 1, null, "", null);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, fileDate, 4242, values)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.True(Assert.Single(results).Succeeded);
        _repository.Verify(r => r.UpdateTagFieldsAndFileInfoAsync(1, values, fileDate.DateTime, 4242, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(l => l.Count == 1 && l[0].FilePath == MusicFile && l[0].Changes.Title == "New"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "when_the_write_succeeds_the_database_receives_the_values_read_back_from_the_file")]
    public async Task UpdateAsync_WriteSucceeds_StoresReadBackValues()
    {
        // Arrange
        DateTimeOffset fileDate = new(2025, 3, 4, 5, 6, 7, TimeSpan.Zero);
        TagFieldValues readBack = new("Read title", 7, 2, "", 128);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, fileDate, 4242, readBack)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, TrackNumber: 8, Composers: "")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.True(result.Succeeded);
        _repository.Verify(r => r.UpdateTagFieldsAndFileInfoAsync(1, readBack, fileDate.DateTime, 4242, It.IsAny<RepositoryConnectionKind>()), Times.Once);
        Assert.Equal(new TrackEditSaved("Read title", 7, 2, null, 128, fileDate.DateTime, 4242), result.Saved);
    }

    [Fact(DisplayName = "when_the_edit_changes_several_fields_the_writer_receives_all_of_them")]
    public async Task UpdateAsync_SeveralFields_SendsEveryChangeToTheWriter()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);

        // Act
        await _sut.UpdateAsync([new TrackTagsEdit(1, "T", 3, 2, "Bach", 90)], CancellationToken.None);

        // Assert
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(l => l[0].Changes.Title == "T" && l[0].Changes.TrackNumber == 3 && l[0].Changes.Disc == 2 && l[0].Changes.Composers == "Bach" && l[0].Changes.Bpm == 90), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "when_the_writer_returns_no_values_the_result_is_failed")]
    public async Task UpdateAsync_WriterReturnsNoValues_ReturnsFailed()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UtcNow, 10)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.IoError, result.Reason);
        Assert.Null(result.Saved);
        VerifyDatabaseNeverUpdated();
    }

    [Fact(DisplayName = "when_an_edit_has_no_field_it_is_skipped_without_touching_the_writer_or_the_database")]
    public async Task UpdateAsync_EditWithoutField_IsSkipped()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1), new TrackTagsEdit(2, "New")], CancellationToken.None);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, results[0].Outcome);
        Assert.Equal(ETagWriteReason.Cancelled, results[0].Reason);
        Assert.True(results[1].Succeeded);
        _writer.Verify(w => w.WriteAsync(It.Is<IReadOnlyList<TagWriteRequest>>(l => l.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.UpdateTagFieldsAndFileInfoAsync(1, It.IsAny<TagFieldValues>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "when_the_writer_returns_no_values_the_result_is_failed_and_the_error_says_the_file_is_written")]
    public async Task UpdateAsync_WriterReturnsNoValues_LogsErrorAboutWrittenFile()
    {
        // Arrange
        Mock<ILogger<TrackTagEditService>> logger = new();
        TrackTagEditService sut = new(_repository.Object, _writer.Object, _player.Object, _import.Object, _gate, _options.Object, logger.Object);
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TagWriteResult(MusicFile, ETagWriteOutcome.Succeeded, ETagWriteReason.None, DateTimeOffset.UtcNow, 10)]);

        // Act
        await sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => $"{state}".Contains("already written") && $"{state}".Contains("rescan")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact(DisplayName = "when_the_writer_skips_a_file_the_database_is_not_updated")]
    public async Task UpdateAsync_WriterSkips_DoesNotUpdateDatabase()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Skipped(MusicFile, ETagWriteReason.Locked)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteReason.Locked, result.Reason);
        Assert.Equal("track.tag_write.locked", result.ErrorCode);
        VerifyDatabaseNeverUpdated();
    }

    [Fact(DisplayName = "when_the_database_update_fails_after_a_successful_write_the_result_is_failed")]
    public async Task UpdateAsync_DatabaseFails_ReturnsFailed()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);
        _repository.Setup(r => r.UpdateTagFieldsAndFileInfoAsync(It.IsAny<long>(), It.IsAny<TagFieldValues>(), It.IsAny<DateTime>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()))
            .ThrowsAsync(new InvalidOperationException("db"));

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        TrackTagEditResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.IoError, result.Reason);
    }

    [Fact(DisplayName = "when_a_batch_mixes_cases_results_keep_the_input_order")]
    public async Task UpdateAsync_MixedBatch_KeepsOrder()
    {
        // Arrange
        _repository.Setup(r => r.GetByIdAsync(2, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync((TrackEntity?)null);
        _repository.Setup(r => r.GetByIdAsync(3, It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(new TrackEntity { Id = 3, MusicFile = @"C:\music\other.mp3" });
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10), Skipped(@"C:\music\other.mp3", ETagWriteReason.ReadOnly)]);

        // Act
        IReadOnlyList<TrackTagEditResult> results = await _sut.UpdateAsync([new TrackTagsEdit(1, "A"), new TrackTagsEdit(2, "B"), new TrackTagsEdit(3, "C")], CancellationToken.None);

        // Assert
        Assert.Equal([1L, 2L, 3L], results.Select(r => r.TrackId));
        Assert.Equal(ETagWriteOutcome.Succeeded, results[0].Outcome);
        Assert.Equal(ETagWriteReason.NotFound, results[1].Reason);
        Assert.Equal(ETagWriteReason.ReadOnly, results[2].Reason);
    }

    [Fact(DisplayName = "after_the_batch_the_gate_can_be_entered_again")]
    public async Task UpdateAsync_AfterBatch_ReleasesGate()
    {
        // Arrange
        _writer.Setup(w => w.WriteAsync(It.IsAny<IReadOnlyList<TagWriteRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Succeeded(MusicFile, DateTimeOffset.UtcNow, 10)]);

        // Act
        await _sut.UpdateAsync([new TrackTagsEdit(1, "New")], CancellationToken.None);

        // Assert
        Assert.True(_gate.TryEnter(out IDisposable? lease));
        lease.Dispose();
    }
}