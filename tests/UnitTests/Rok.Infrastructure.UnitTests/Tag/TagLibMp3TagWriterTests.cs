using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibMp3TagWriterTests : IDisposable
{
    private const string NewTitle = "A brand new title";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibMp3TagWriterTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibMp3TagWriterTests()
    {
        _sut = new TagLibTagWriter(JournalFolder, NullLogger<TagLibTagWriter>.Instance, _files);
    }

    private string JournalFolder => Path.Combine(_tempDir.FullName, "journal");

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        foreach (FileInfo file in _tempDir.EnumerateFiles("*", SearchOption.AllDirectories))
            file.IsReadOnly = false;

        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_set_the_id3v2_title_of_an_mp3")]
    public async Task WriteAsync_ShouldSetTheTitle_OfAnMp3()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(ETagWriteReason.None, result.Reason);
        Assert.Equal(NewTitle, ReadId3v2(path).Title);
        Assert.Equal("Original artist", ReadId3v2(path).FirstPerformer);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_leave_the_audio_stream_byte_identical")]
    public async Task WriteAsync_ShouldLeaveTheAudioStreamIdentical()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        string hashBefore = HashAudio(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_keep_every_other_id3v2_frame")]
    public async Task WriteAsync_ShouldKeepEveryOtherFrame()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        List<string> framesBefore = RenderFramesExceptTitle(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(framesBefore, RenderFramesExceptTitle(path));

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        string[] ids = [.. tag.GetFrames().Select(f => f.FrameId.ToString())];
        Assert.Contains("APIC", ids);
        Assert.Contains("USLT", ids);
        Assert.Contains(Mp3TagFixture.UnknownFrameId, ids);
        Assert.Equal(2, ids.Count(id => id == "TXXX"));
        Assert.Equal(Mp3TagFixture.PictureBytes, tag.Pictures[0].Data.Data);
        Assert.Equal(Mp3TagFixture.LyricsText, tag.Lyrics);
        Assert.Equal(Mp3TagFixture.MusicBrainzValue, tag.MusicBrainzReleaseId);
        Assert.Equal(Mp3TagFixture.UnknownFrameBytes, tag.GetFrames<TagLib.Id3v2.UnknownFrame>().Single().Data.Data);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "write_should_keep_the_id3v2_version")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldKeepTheId3v2Version(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, version);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(version, ReadId3v2(path).Version);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_create_an_id3v2_3_tag_when_the_file_has_none")]
    public async Task WriteAsync_ShouldCreateId3v23Tag_WhenFileHasNone()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithoutTags(MusicFolder);
        string hashBefore = HashAudio(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        Assert.Equal(3, tag.Version);
        Assert.Equal(NewTitle, tag.Title);
        Assert.Equal(hashBefore, HashAudio(path));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_sync_an_existing_id3v1_tag")]
    public async Task WriteAsync_ShouldSyncExistingId3v1Tag()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithId3v1(MusicFolder);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal(NewTitle, file.GetTag(TagLib.TagTypes.Id3v1, false)?.Title);
        Assert.Equal(NewTitle, file.GetTag(TagLib.TagTypes.Id3v2, false)?.Title);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_not_create_id3v1_or_ape_tags")]
    public async Task WriteAsync_ShouldNotCreateId3v1OrApeTags()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal(TagLib.TagTypes.Id3v2, file.TagTypesOnDisk);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_refuse_an_id3v2_2_tag_and_leave_the_file_untouched")]
    public async Task WriteAsync_ShouldRefuseId3v22Tag()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithId3v22(MusicFolder);
        byte[] before = await File.ReadAllBytesAsync(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.UnsupportedTagVersion, result.Reason);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_report_the_file_date_and_size_read_after_the_replacement")]
    public async Task WriteAsync_ShouldReportFileDateAndSize_ReadAfterReplacement()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        FileInfo info = new(path);
        Assert.Equal(info.LastWriteTimeUtc.Ticks, result.FileDateModified!.Value.UtcDateTime.Ticks);
        Assert.Equal(info.Length, result.Size);
    }

    [Theory(DisplayName = "write_should_skip_unsupported_formats_and_leave_them_untouched")]
    [InlineData(".wma")]
    [InlineData(".wav")]
    [InlineData(".aiff")]
    [InlineData(".aif")]
    [InlineData(".aac")]
    public async Task WriteAsync_ShouldSkipUnsupportedFormats(string extension)
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string path = Path.Combine(MusicFolder, "song" + extension);
        byte[] content = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(path, content);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.UnsupportedFormat, result.Reason);
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [Fact(DisplayName = "write_should_skip_a_missing_file")]
    public async Task WriteAsync_ShouldSkipMissingFile()
    {
        // Arrange
        string path = Path.Combine(MusicFolder, "missing.mp3");

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.NotFound, result.Reason);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_skip_a_read_only_file")]
    public async Task WriteAsync_ShouldSkipReadOnlyFile()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        byte[] before = await File.ReadAllBytesAsync(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.ReadOnly, result.Reason);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_skip_a_locked_file_and_leave_no_temporary_file")]
    public async Task WriteAsync_ShouldSkipLockedFile()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        using FileStream held = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.Locked, result.Reason);
        Assert.Empty(TemporaryFiles());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_fail_and_keep_the_original_when_the_copy_does_not_verify")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenCopyDoesNotVerify()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.AfterCopy = (_, copy) => CorruptLastByte(copy);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.VerificationFailed, result.Reason);
        Assert.Equal(SHA256.HashData(before), SHA256.HashData(await File.ReadAllBytesAsync(path)));
        Assert.Empty(TemporaryFiles());
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_fall_back_to_copy_and_rename_when_file_replace_fails")]
    public async Task WriteAsync_ShouldFallBackToCopyAndRename_WhenReplaceFails()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        _files.ReplaceOverride = (_, _) => throw new IOException("Replace is not available.");

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(NewTitle, ReadId3v2(path).Title);
        Assert.Empty(TemporaryFiles());
        Assert.Empty(new TagWriteJournal(JournalFolder).GetEntries());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_return_one_result_per_request_in_order_and_continue_after_a_failure")]
    public async Task WriteAsync_ShouldReturnOneResultPerRequest_InOrder()
    {
        // Arrange
        string good = Mp3TagFixture.CreateRich(Path.Combine(MusicFolder, "a"), 3);
        string missing = Path.Combine(MusicFolder, "b", "missing.mp3");
        string corrupt = Path.Combine(MusicFolder, "c", "corrupt.mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(corrupt)!);
        await File.WriteAllBytesAsync(corrupt, new byte[2048]);

        // Act
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync(
            [Request(good), Request(missing), Request(corrupt)],
            CancellationToken.None);

        // Assert
        Assert.Equal([good, missing, corrupt], results.Select(r => r.FilePath));
        Assert.Equal(ETagWriteOutcome.Succeeded, results[0].Outcome);
        Assert.Equal(ETagWriteOutcome.Skipped, results[1].Outcome);
        Assert.Equal(ETagWriteReason.NotFound, results[1].Reason);
        Assert.Equal(ETagWriteOutcome.Failed, results[2].Outcome);
        Assert.Equal(ETagWriteReason.IoError, results[2].Reason);
        Assert.Empty(TemporaryFiles());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_should_stop_between_two_files_when_cancelled")]
    public async Task WriteAsync_ShouldStopBetweenTwoFiles_WhenCancelled()
    {
        // Arrange
        string first = Mp3TagFixture.CreateRich(Path.Combine(MusicFolder, "a"), 3);
        string second = Mp3TagFixture.CreateRich(Path.Combine(MusicFolder, "b"), 3);
        byte[] secondBefore = await File.ReadAllBytesAsync(second);
        using CancellationTokenSource cts = new();
        DefaultTagWriteFileOperations real = new();

        _files.ReplaceOverride = (source, destination) =>
        {
            real.Replace(source, destination);
            cts.Cancel();
        };

        // Act
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync([Request(first), Request(second)], cts.Token);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, results[0].Outcome);
        Assert.Equal(NewTitle, ReadId3v2(first).Title);
        Assert.Equal(ETagWriteOutcome.Skipped, results[1].Outcome);
        Assert.Equal(ETagWriteReason.Cancelled, results[1].Reason);
        Assert.Equal(secondBefore, await File.ReadAllBytesAsync(second));
        Assert.Empty(TemporaryFiles());
    }

    private async Task<TagWriteResult> WriteSingleAsync(string path)
    {
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync([Request(path)], CancellationToken.None);

        return Assert.Single(results);
    }

    private static TagWriteRequest Request(string path)
    {
        return new TagWriteRequest(path, new TagChanges { Title = NewTitle });
    }

    private IEnumerable<string> TemporaryFiles()
    {
        if (!Directory.Exists(MusicFolder))
            return [];

        return Directory.EnumerateFiles(MusicFolder, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".roktmp", StringComparison.Ordinal) || f.EndsWith(".rokold", StringComparison.Ordinal));
    }

    private static TagLib.File OpenMp3(string path)
    {
        return TagLib.File.Create(path, "taglib/mp3", TagLib.ReadStyle.Average);
    }

    private static TagLib.Id3v2.Tag ReadId3v2(string path)
    {
        using TagLib.File file = OpenMp3(path);

        return (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, false);
    }

    private static List<string> RenderFramesExceptTitle(string path)
    {
        TagLib.Id3v2.Tag tag = ReadId3v2(path);

        return [.. tag.GetFrames()
            .Where(f => f.FrameId.ToString() != "TIT2")
            .Select(f => Convert.ToHexString(f.Render(tag.Version).Data))];
    }

    private static string HashAudio(string path)
    {
        long start;
        long end;

        using (TagLib.File file = OpenMp3(path))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
        }

        byte[] bytes = File.ReadAllBytes(path);

        return Convert.ToHexString(SHA256.HashData(bytes.AsSpan((int)start, (int)(end - start))));
    }

    private static void CorruptLastByte(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }
}