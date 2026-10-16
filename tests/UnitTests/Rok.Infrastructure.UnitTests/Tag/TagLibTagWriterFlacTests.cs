using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterFlacTests : IDisposable
{
    private const string NewTitle = "A brand new title";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterFlacTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterFlacTests()
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

    [Fact(DisplayName = "flac_write_should_set_the_xiph_title")]
    public async Task WriteAsync_ShouldSetTheXiphTitle()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(ETagWriteReason.None, result.Reason);

        TagLib.Ogg.XiphComment xiph = ReadXiph(path);
        Assert.Equal(NewTitle, xiph.Title);
        Assert.Equal(FlacTagFixture.OriginalArtist, xiph.FirstPerformer);
    }

    [Fact(DisplayName = "flac_write_should_leave_the_audio_frames_byte_identical")]
    public async Task WriteAsync_ShouldLeaveAudioFramesIdentical()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);
        string hashBefore = HashFrames(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashFrames(path));
    }

    [Fact(DisplayName = "flac_write_should_decode_to_the_same_samples")]
    public async Task WriteAsync_ShouldDecodeToTheSameSamples()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);
        string samplesBefore = HashDecodedSamples(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(samplesBefore, HashDecodedSamples(path));
    }

    [Fact(DisplayName = "flac_write_should_keep_picture_lyrics_mbid_replaygain_and_unknown_field")]
    public async Task WriteAsync_ShouldKeepOtherFields()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, false);

        Assert.Equal(FlacTagFixture.LyricsText, xiph.GetFirstField("LYRICS"));
        Assert.Equal(FlacTagFixture.MusicBrainzValue, xiph.GetFirstField("MUSICBRAINZ_ALBUMID"));
        Assert.Equal(FlacTagFixture.ReplayGainValue, xiph.GetFirstField("REPLAYGAIN_TRACK_GAIN"));
        Assert.Equal(FlacTagFixture.UnknownFieldValue, xiph.GetFirstField("ROK_UNKNOWN_FIELD"));
        Assert.Equal(FlacTagFixture.LowercaseFieldValue, xiph.GetFirstField(FlacTagFixture.LowercaseFieldName));
        Assert.Equal(FlacTagFixture.PictureBytes, file.GetTag(TagLib.TagTypes.FlacMetadata, false).Pictures.Single().Data.Data);

        FlacLayoutInfo layout = FlacLayout.Read(path);
        FlacMetadataBlock application = Assert.Single(layout.Blocks, b => b.Type == FlacBlock.ApplicationType);
        Assert.Equal([.. System.Text.Encoding.ASCII.GetBytes(FlacTagFixture.ApplicationId), .. FlacTagFixture.ApplicationPayload], application.Data);
    }

    [Fact(DisplayName = "flac_write_should_succeed_with_a_title_longer_than_the_padding")]
    public async Task WriteAsync_ShouldSucceed_WithATitleLongerThanThePadding()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);
        string longTitle = new('x', 2000);
        string hashBefore = HashFrames(path);
        (TimeSpan duration, int bitrate) = ReadProperties(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, longTitle);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(longTitle, ReadXiph(path).Title);
        Assert.Equal((duration, bitrate), ReadProperties(path));
        Assert.Equal(hashBefore, HashFrames(path));
    }

    [Fact(DisplayName = "flac_write_should_create_a_xiph_comment_when_the_file_has_none")]
    public async Task WriteAsync_ShouldCreateXiphComment_WhenFileHasNone()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithoutTags(MusicFolder);
        string hashBefore = HashFrames(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(NewTitle, ReadXiph(path).Title);
        Assert.Single(FlacLayout.Read(path).Blocks, b => b.Type == FlacLayout.VorbisCommentType);
        Assert.Equal(hashBefore, HashFrames(path));
    }

    [Fact(DisplayName = "flac_write_should_sync_a_parasitic_id3v2_title_and_keep_its_frames")]
    public async Task WriteAsync_ShouldSyncParasiticId3v2_AndKeepItsFrames()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithParasiticId3v2(MusicFolder, 3);
        string hashBefore = HashFrames(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, false);

        Assert.Equal(NewTitle, file.GetTag(TagLib.TagTypes.Xiph, false).Title);
        Assert.Equal(NewTitle, id3v2.Title);
        Assert.Equal(FlacTagFixture.OriginalArtist, id3v2.FirstPerformer);
        Assert.Equal(FlacTagFixture.MusicBrainzValue, id3v2.MusicBrainzReleaseId);
        Assert.Equal(hashBefore, HashFrames(path));
    }

    [Fact(DisplayName = "flac_write_should_refuse_a_parasitic_id3v2_2_tag")]
    public async Task WriteAsync_ShouldRefuseParasiticId3v22Tag()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithParasiticId3v2(MusicFolder, 2);
        byte[] before = await File.ReadAllBytesAsync(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.UnsupportedTagVersion, result.Reason);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [Fact(DisplayName = "flac_write_should_fail_on_a_corrupt_file_and_leave_it_untouched")]
    public async Task WriteAsync_ShouldFailOnCorruptFile_AndLeaveItUntouched()
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string path = Path.Combine(MusicFolder, "corrupt.flac");
        byte[] content = new byte[2048];
        await File.WriteAllBytesAsync(path, content);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.IoError, result.Reason);
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [Fact(DisplayName = "flac_write_should_fail_and_keep_the_original_when_the_copy_does_not_verify")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenCopyDoesNotVerify()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);
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

    [Fact(DisplayName = "flac_write_should_report_the_file_date_and_size_read_after_the_replacement")]
    public async Task WriteAsync_ShouldReportFileDateAndSize_ReadAfterReplacement()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        FileInfo info = new(path);
        Assert.Equal(info.LastWriteTimeUtc.Ticks, result.FileDateModified!.Value.UtcDateTime.Ticks);
        Assert.Equal(info.Length, result.Size);
    }

    private async Task<TagWriteResult> WriteSingleAsync(string path, string title = NewTitle)
    {
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync([new TagWriteRequest(path, new TagChanges { Title = title })], CancellationToken.None);

        return Assert.Single(results);
    }

    private IEnumerable<string> TemporaryFiles()
    {
        if (!Directory.Exists(MusicFolder))
            return [];

        return Directory.EnumerateFiles(MusicFolder, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".roktmp", StringComparison.Ordinal) || f.EndsWith(".rokold", StringComparison.Ordinal));
    }

    private static TagLib.File OpenFlac(string path)
    {
        return TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average);
    }

    private static TagLib.Ogg.XiphComment ReadXiph(string path)
    {
        using TagLib.File file = OpenFlac(path);

        return (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, false);
    }

    private static (TimeSpan Duration, int Bitrate) ReadProperties(string path)
    {
        using TagLib.File file = OpenFlac(path);

        return (file.Properties.Duration, file.Properties.AudioBitrate);
    }

    private static string HashFrames(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int position = 0;

        while (bytes.AsSpan(position, 3).SequenceEqual("ID3"u8))
            position += 10 + (bytes[position + 6] << 21 | bytes[position + 7] << 14 | bytes[position + 8] << 7 | bytes[position + 9]);

        position += 4;

        bool last;

        do
        {
            last = (bytes[position] & 0x80) != 0;
            position += 4 + (bytes[position + 1] << 16 | bytes[position + 2] << 8 | bytes[position + 3]);
        }
        while (!last);

        return Convert.ToHexString(SHA256.HashData(bytes.AsSpan(position)));
    }

    private static string HashDecodedSamples(string path)
    {
        using AudioFileReader reader = new(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        float[] buffer = new float[4096];
        int read;

        while ((read = reader.Read(buffer.AsSpan())) > 0)
            hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(buffer.AsSpan(0, read)));

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void CorruptLastByte(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }
}