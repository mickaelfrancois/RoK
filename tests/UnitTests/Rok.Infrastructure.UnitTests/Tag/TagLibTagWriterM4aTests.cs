using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterM4aTests : IDisposable
{
    private const string NewTitle = "A brand new title";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterM4aTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterM4aTests()
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

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_set_the_nam_atom")]
    public async Task WriteAsync_ShouldSetTheNamAtom()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(ETagWriteReason.None, result.Reason);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(NewTitle, file.GetTag(TagLib.TagTypes.Apple, false).Title);
        Assert.Equal(M4aTagFixture.OriginalArtist, file.GetTag(TagLib.TagTypes.Apple, false).FirstPerformer);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_keep_the_media_boxes_identical_when_moov_is_before_mdat")]
    public async Task WriteAsync_ShouldKeepMediaBoxesIdentical_WhenMoovIsBeforeMdat()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
        string mediaBefore = HashMediaBoxes(path);
        string offsetsBefore = Mp4Layout.HashRelativeChunkOffsets(path);
        long mdatBefore = FindRootBoxStart(path, "mdat");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new string('x', 5000));

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(EMp4Layout.MoovBeforeMdat, Mp4LayoutRewriter.GetLayout(path));
        Assert.NotEqual(mdatBefore, FindRootBoxStart(path, "mdat"));
        Assert.Equal(mediaBefore, HashMediaBoxes(path));
        Assert.Equal(offsetsBefore, Mp4Layout.HashRelativeChunkOffsets(path));
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_decode_to_the_same_samples")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public async Task WriteAsync_ShouldDecodeToTheSameSamples(EMp4Layout layout)
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, layout);
        string samplesBefore = HashDecodedSamples(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new string('x', 5000));

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(samplesBefore, HashDecodedSamples(path));
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_keep_cover_lyrics_mbid_replaygain_and_unknown_atom")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public async Task WriteAsync_ShouldKeepOtherAtoms(EMp4Layout layout)
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, layout);
        IReadOnlyList<Mp4IlstItem> before = Mp4Layout.ReadIlstItems(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        TagLib.Mpeg4.AppleTag tag = (TagLib.Mpeg4.AppleTag)file.GetTag(TagLib.TagTypes.Apple, false);

        Assert.Equal(M4aTagFixture.LyricsText, tag.Lyrics);
        Assert.Equal(M4aTagFixture.PictureBytes, tag.Pictures.Single().Data.Data);
        Assert.Equal(M4aTagFixture.MusicBrainzValue, tag.GetDashBox(M4aTagFixture.DashMean, M4aTagFixture.MusicBrainzName));
        Assert.Equal(M4aTagFixture.ReplayGainValue, tag.GetDashBox(M4aTagFixture.DashMean, M4aTagFixture.ReplayGainName));
        Assert.Equal(M4aTagFixture.UnknownAtomValue, Assert.Single(tag.GetText(new TagLib.ByteVector(M4aTagFixture.UnknownAtomType))));

        IReadOnlyList<Mp4IlstItem> after = Mp4Layout.ReadIlstItems(path);
        Assert.Equal(
            OrderedHashes(before),
            OrderedHashes(after));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_succeed_with_a_long_title")]
    public async Task WriteAsync_ShouldSucceed_WithALongTitle()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
        string longTitle = new('x', 20000);
        string mediaBefore = HashMediaBoxes(path);
        string offsetsBefore = Mp4Layout.HashRelativeChunkOffsets(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, longTitle);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(longTitle, ReadTitle(path));
        Assert.Equal(mediaBefore, HashMediaBoxes(path));
        Assert.Equal(offsetsBefore, Mp4Layout.HashRelativeChunkOffsets(path));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_create_an_apple_tag_when_the_file_has_none")]
    public async Task WriteAsync_ShouldCreateAppleTag_WhenFileHasNone()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithoutTags(MusicFolder, EMp4Layout.MoovBeforeMdat);
        string mediaBefore = HashMediaBoxes(path);
        string samplesBefore = HashDecodedSamples(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(NewTitle, ReadTitle(path));
        Assert.Equal(mediaBefore, HashMediaBoxes(path));
        Assert.Equal(samplesBefore, HashDecodedSamples(path));
    }

    [Fact(DisplayName = "m4a_write_should_refuse_a_fragmented_mp4")]
    public async Task WriteAsync_ShouldRefuseFragmentedMp4()
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string path = Path.Combine(MusicFolder, "fragmented.m4a");
        byte[] content =
        [
            .. Box("ftyp", [.. "M4A "u8, 0, 0, 0, 0, .. "M4A isom"u8]),
            .. Box("moov", new byte[16]),
            .. Box("moof", new byte[16]),
            .. Box("mdat", new byte[32])
        ];
        await File.WriteAllBytesAsync(path, content);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Skipped, result.Outcome);
        Assert.Equal(ETagWriteReason.UnsupportedFormat, result.Reason);
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [Fact(DisplayName = "m4a_write_should_fail_on_a_corrupt_file_and_leave_it_untouched")]
    public async Task WriteAsync_ShouldFailOnCorruptFile_AndLeaveItUntouched()
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string path = Path.Combine(MusicFolder, "corrupt.m4a");
        byte[] content = [.. Box("ftyp", new byte[12]), 0, 0, 255, 255, .. "mdat"u8, 1, 2, 3];
        await File.WriteAllBytesAsync(path, content);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.IoError, result.Reason);
        Assert.Equal(content, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporaryFiles());
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_fail_and_keep_the_original_when_the_copy_does_not_verify")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenCopyDoesNotVerify()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
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

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_report_the_file_date_and_size_read_after_the_replacement")]
    public async Task WriteAsync_ShouldReportFileDateAndSize_ReadAfterReplacement()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        FileInfo info = new(path);
        Assert.Equal(info.LastWriteTimeUtc.Ticks, result.FileDateModified!.Value.UtcDateTime.Ticks);
        Assert.Equal(info.Length, result.Size);
    }

    [AlacEncoderFact(DisplayName = "m4a_write_should_accept_alac")]
    public async Task WriteAsync_ShouldAcceptAlac()
    {
        // Arrange
        Directory.CreateDirectory(MusicFolder);
        string path = Path.Combine(MusicFolder, "alac.m4a");
        AlacEncoderFactAttribute.Encode(path);
        string mediaBefore = HashMediaBoxes(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(NewTitle, ReadTitle(path));
        Assert.Equal(mediaBefore, HashMediaBoxes(path));
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

    private static TagLib.File OpenM4a(string path)
    {
        return TagLib.File.Create(path, "taglib/m4a", TagLib.ReadStyle.Average);
    }

    private static string? ReadTitle(string path)
    {
        using TagLib.File file = OpenM4a(path);

        return file.GetTag(TagLib.TagTypes.Apple, false)?.Title;
    }

    private static string[] OrderedHashes(IReadOnlyList<Mp4IlstItem> items)
    {
        return items
            .Where(i => i.Type != Mp4Layout.TitleAtomType)
            .Select(i => $"{i.Type}:{Convert.ToHexString(SHA256.HashData(i.Data))}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static List<(string Type, long Start, long Size)> ReadRootBoxes(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        List<(string, long, long)> boxes = [];
        long position = 0;

        while (position + 8 <= bytes.Length)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan((int)position, 4));
            string type = Encoding.Latin1.GetString(bytes, (int)position + 4, 4);

            if (size == 1)
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan((int)position + 8, 8));
            else if (size == 0)
                size = bytes.Length - position;

            boxes.Add((type, position, size));
            position += size;
        }

        return boxes;
    }

    private static long FindRootBoxStart(string path, string type)
    {
        return ReadRootBoxes(path).First(b => b.Type == type).Start;
    }

    private static string HashMediaBoxes(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach ((string type, long start, long size) in ReadRootBoxes(path))
        {
            if (type is "moov" or "free" or "skip" or "wide")
                continue;

            hash.AppendData(bytes.AsSpan((int)start, (int)size));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string HashDecodedSamples(string path)
    {
        MediaFoundationApi.Startup();

        using MediaFoundationReader reader = new(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        long total = 0;
        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            total += read;
        }

        Assert.True(total > 0, "Decoded PCM is empty.");

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static byte[] Box(string type, byte[] body)
    {
        byte[] box = new byte[8 + body.Length];

        BinaryPrimitives.WriteUInt32BigEndian(box, (uint)box.Length);
        Encoding.Latin1.GetBytes(type, box.AsSpan(4, 4));
        body.CopyTo(box, 8);

        return box;
    }

    private static void CorruptLastByte(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    private sealed class AlacEncoderFactAttribute : FactAttribute
    {
        private const int SampleRate = 44100;
        private const int Channels = 2;

        public AlacEncoderFactAttribute()
        {
            if (!IsAvailable())
                Skip = "No Media Foundation ALAC encoder available on this machine.";
        }

        public static void Encode(string path)
        {
            MediaFoundationApi.Startup();

            string scratch = Path.Combine(Path.GetTempPath(), "AlacFixture_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);

            try
            {
                string wav = AudioFixtureFactory.Create(scratch, AudioFixtureFormat.Wav);
                using WaveFileReader reader = new(wav);
                MediaType mediaType = MediaFoundationEncoder.SelectMediaType(AudioSubtypes.MFAudioFormat_ALAC, reader.WaveFormat, 0)
                    ?? throw new InvalidOperationException("No ALAC media type.");

                using MediaFoundationEncoder encoder = new(mediaType);
                encoder.Encode(path, reader);
            }
            finally
            {
                Directory.Delete(scratch, recursive: true);
            }
        }

        private static bool IsAvailable()
        {
            try
            {
                MediaFoundationApi.Startup();

                if (MediaFoundationEncoder.SelectMediaType(AudioSubtypes.MFAudioFormat_ALAC, new WaveFormat(SampleRate, 16, Channels), 0) is null)
                    return false;

                string probe = Path.Combine(Path.GetTempPath(), "AlacProbe_" + Guid.NewGuid().ToString("N") + ".m4a");

                try
                {
                    Encode(probe);

                    return true;
                }
                finally
                {
                    File.Delete(probe);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}