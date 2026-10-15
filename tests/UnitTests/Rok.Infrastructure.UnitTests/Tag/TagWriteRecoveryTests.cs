using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagWriteRecoveryTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagWriteRecoveryTests_");
    private readonly FaultInjectingFileOperations _files = new();

    private string JournalFolder => Path.Combine(_tempDir.FullName, "journal");

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "recover_should_remove_the_temporary_copy_left_between_copy_and_replace")]
    public async Task RecoverAsync_ShouldRemoveTemporaryCopy_LeftBetweenCopyAndReplace()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.ReplaceOverride = (_, _) =>
        {
            _files.Crash();
            throw new InvalidOperationException("Simulated crash.");
        };

        await CreateWriter(_files).WriteAsync([Request(path)], CancellationToken.None);

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Leftovers(".roktmp"));
        Assert.Single(new TagWriteJournal(JournalFolder).GetEntries());

        // Act
        await CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers(".roktmp"));
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "recover_should_restore_the_original_after_an_interrupted_fallback")]
    public async Task RecoverAsync_ShouldRestoreOriginal_AfterInterruptedFallback()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.ReplaceOverride = (_, _) => throw new IOException("Replace is not available.");
        _files.BeforeMove = (source, destination) =>
        {
            if (source.EndsWith(".roktmp", StringComparison.Ordinal) && destination == path)
                _files.Crash();
        };

        await CreateWriter(_files).WriteAsync([Request(path)], CancellationToken.None);

        Assert.False(File.Exists(path));
        Assert.Single(Leftovers(".rokold"));
        Assert.Single(Leftovers(".roktmp"));

        // Act
        await CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers(".rokold"));
        Assert.Empty(Leftovers(".roktmp"));
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    [Fact(DisplayName = "recover_should_remove_the_temporary_copy_left_between_copy_and_replace_flac")]
    public Task RecoverAsync_ShouldRemoveTemporaryCopy_LeftBetweenCopyAndReplace_Flac()
    {
        return AssertTemporaryCopyRemovedAsync(FlacTagFixture.CreateRich(MusicFolder));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "recover_should_remove_the_temporary_copy_left_between_copy_and_replace_m4a")]
    public Task RecoverAsync_ShouldRemoveTemporaryCopy_LeftBetweenCopyAndReplace_M4a()
    {
        return AssertTemporaryCopyRemovedAsync(M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat));
    }

    [Fact(DisplayName = "recover_should_restore_the_original_after_an_interrupted_fallback_flac")]
    public Task RecoverAsync_ShouldRestoreOriginal_AfterInterruptedFallback_Flac()
    {
        return AssertOriginalRestoredAsync(FlacTagFixture.CreateRich(MusicFolder));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "recover_should_restore_the_original_after_an_interrupted_fallback_m4a")]
    public Task RecoverAsync_ShouldRestoreOriginal_AfterInterruptedFallback_M4a()
    {
        return AssertOriginalRestoredAsync(M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat));
    }

    [Fact(DisplayName = "recover_should_do_nothing_when_there_is_no_journal")]
    public async Task RecoverAsync_ShouldDoNothing_WhenThereIsNoJournal()
    {
        // Act
        Exception? error = await Record.ExceptionAsync(() => CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None));

        // Assert
        Assert.Null(error);
    }

    [Fact(DisplayName = "recover_should_keep_the_journal_entry_when_the_cleanup_fails")]
    public async Task RecoverAsync_ShouldKeepJournalEntry_WhenCleanupFails()
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string original = Path.Combine(MusicFolder, "song.mp3");
        string temp = original + ".abc.roktmp";
        await File.WriteAllBytesAsync(original, [1]);
        await File.WriteAllBytesAsync(temp, [2]);
        new TagWriteJournal(JournalFolder).Add(new TagWriteJournalEntry(original, temp, original + ".abc.rokold"));
        _files.Crash();

        // Act
        await CreateWriter(_files).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.True(File.Exists(temp));
        Assert.Single(new TagWriteJournal(JournalFolder).GetEntries());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_succeed_and_recover_should_clean_up_when_the_backup_deletion_throws")]
    public async Task WriteAsync_ShouldSucceed_WhenBackupDeletionThrows()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        _files.ReplaceOverride = (_, _) => throw new IOException("Replace is not available.");
        _files.DeleteOverride = _ => throw new InvalidOperationException("Delete is broken.");

        // Act
        IReadOnlyList<TagWriteResult> results = await CreateWriter(_files).WriteAsync([Request(path)], CancellationToken.None);

        // Assert
        TagWriteResult result = Assert.Single(results);
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.NotNull(result.FileDateModified);
        Assert.Equal(new FileInfo(path).Length, result.Size);
        Assert.Single(Leftovers(".rokold"));
        Assert.Single(new TagWriteJournal(JournalFolder).GetEntries());

        using (TagLib.File file = TagLib.File.Create(path))
            Assert.Equal("A brand new title", file.Tag.Title);

        // Act
        await CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.Empty(Leftovers(".rokold"));
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());

        using (TagLib.File file = TagLib.File.Create(path))
            Assert.Equal("A brand new title", file.Tag.Title);
    }

    private async Task AssertTemporaryCopyRemovedAsync(string path)
    {
        // Arrange
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.ReplaceOverride = (_, _) =>
        {
            _files.Crash();
            throw new InvalidOperationException("Simulated crash.");
        };

        await CreateWriter(_files).WriteAsync([Request(path)], CancellationToken.None);

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Leftovers(".roktmp"));
        Assert.Single(new TagWriteJournal(JournalFolder).GetEntries());

        // Act
        await CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers(".roktmp"));
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    private async Task AssertOriginalRestoredAsync(string path)
    {
        // Arrange
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.ReplaceOverride = (_, _) => throw new IOException("Replace is not available.");
        _files.BeforeMove = (source, destination) =>
        {
            if (source.EndsWith(".roktmp", StringComparison.Ordinal) && destination == path)
                _files.Crash();
        };

        await CreateWriter(_files).WriteAsync([Request(path)], CancellationToken.None);

        Assert.False(File.Exists(path));
        Assert.Single(Leftovers(".rokold"));
        Assert.Single(Leftovers(".roktmp"));

        // Act
        await CreateWriter(new DefaultTagWriteFileOperations()).RecoverAsync(CancellationToken.None);

        // Assert
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(Leftovers(".rokold"));
        Assert.Empty(Leftovers(".roktmp"));
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    private TagLibTagWriter CreateWriter(ITagWriteFileOperations files)
    {
        return new TagLibTagWriter(JournalFolder, NullLogger<TagLibTagWriter>.Instance, files);
    }

    private static TagWriteRequest Request(string path)
    {
        return new TagWriteRequest(path, new TagChanges { Title = "A brand new title" });
    }

    private string[] Leftovers(string extension)
    {
        return Directory.GetFiles(MusicFolder, "*" + extension, SearchOption.AllDirectories);
    }
}