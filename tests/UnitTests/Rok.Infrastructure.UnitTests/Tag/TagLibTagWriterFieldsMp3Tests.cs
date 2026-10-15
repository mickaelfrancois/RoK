using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterFieldsMp3Tests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterFieldsMp3Tests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterFieldsMp3Tests()
    {
        _sut = new TagLibTagWriter(JournalFolder, NullLogger<TagLibTagWriter>.Instance, _files);
    }

    private string JournalFolder => Path.Combine(_tempDir.FullName, "journal");

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_set_track_number_and_keep_the_track_total")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldSetTrackNumberAndKeepTotal(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, version);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 7 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal(7u, file.Tag.Track);
        Assert.Equal(Mp3TagFixture.OriginalTrackCount, file.Tag.TrackCount);
        Assert.Equal(7, result.Values!.TrackNumber);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_set_disc_and_keep_the_disc_total")]
    public async Task WriteAsync_ShouldSetDiscAndKeepTotal()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Disc = 2 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal(2u, file.Tag.Disc);
        Assert.Equal(Mp3TagFixture.OriginalDiscCount, file.Tag.DiscCount);
        Assert.Equal(2, result.Values!.Disc);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_set_composer")]
    public async Task WriteAsync_ShouldSetComposer()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "New composer" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal("New composer", ReadId3v2(path).JoinedComposers);
        Assert.Equal("New composer", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_clear_composer")]
    public async Task WriteAsync_ShouldClearComposer()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain("TCOM", FrameIds(path));
        Assert.Empty(ReadId3v2(path).Composers);
        Assert.Equal("", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_set_bpm")]
    public async Task WriteAsync_ShouldSetBpm()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 98 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(98u, ReadId3v2(path).BeatsPerMinute);
        Assert.Equal(98, result.Values!.Bpm);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_clear_bpm")]
    public async Task WriteAsync_ShouldClearBpm()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 0 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(0u, ReadId3v2(path).BeatsPerMinute);
        Assert.DoesNotContain("TBPM", FrameIds(path));
        Assert.Null(result.Values!.Bpm);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_keep_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        string hashBefore = HashAudio(path);
        List<string> framesBefore = RenderFramesExcept(path, "TRCK");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 9 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
        Assert.Equal(framesBefore, RenderFramesExcept(path, "TRCK"));

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        Assert.Equal(Mp3TagFixture.OriginalTitle, tag.Title);
        Assert.Equal(Mp3TagFixture.OriginalDisc, tag.Disc);
        Assert.Equal(Mp3TagFixture.OriginalComposer, tag.JoinedComposers);
        Assert.Equal(Mp3TagFixture.OriginalBpm, tag.BeatsPerMinute);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_apply_all_fields_in_one_write")]
    public async Task WriteAsync_ShouldApplyAllFields_InOneWrite()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        TagChanges changes = new() { Title = "Title", TrackNumber = 4, Disc = 2, Composers = "Someone", Bpm = 133 };

        // Act
        TagWriteResult result = await WriteSingleAsync(path, changes);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues("Title", 4, 2, "Someone", 133), result.Values);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_sync_existing_ape_and_clear_its_composer")]
    public async Task WriteAsync_ShouldSyncExistingApe_AndClearItsComposer()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithApe(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "", Bpm = 77, TrackNumber = 5 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        TagLib.Tag ape = file.GetTag(TagLib.TagTypes.Ape, false);
        Assert.NotNull(ape);
        Assert.Empty(ape.Composers);
        Assert.Equal(77u, ape.BeatsPerMinute);
        Assert.Equal(5u, ape.Track);
        Assert.True(string.IsNullOrEmpty(file.Tag.JoinedComposers));
        Assert.Equal("", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_v23_composer_with_slash_is_accepted_and_reported_as_read")]
    public async Task WriteAsync_ShouldAcceptSlashComposer_InId3v23_AndReportWhatIsRead()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "AC/DC" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal(file.Tag.JoinedComposers?.Trim() ?? "", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "write_result_values_match_the_file_read_by_the_import")]
    public async Task WriteAsync_ShouldReturnValues_MatchingTheImportRead()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithApe(MusicFolder, 4);
        TagService service = new(NullLogger<TagService>.Instance);
        TrackFile track = new();

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Title = "T", TrackNumber = 6, Disc = 3, Composers = "A; B", Bpm = 140 });
        service.FillMusicProperties(path, track);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues(track.Title, track.TrackNumber ?? 0, track.Disc, track.Composers, track.Bpm), result.Values);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_should_fail_and_keep_the_original_when_a_field_edit_does_not_verify")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenFieldEditDoesNotVerify()
    {
        // Arrange
        string path = Mp3TagFixture.CreateRich(MusicFolder, 3);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.AfterCopy = (_, copy) => CorruptLastByte(copy);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 99, Composers = "X" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.VerificationFailed, result.Reason);
        Assert.Equal(SHA256.HashData(before), SHA256.HashData(await File.ReadAllBytesAsync(path)));
    }

    private async Task<TagWriteResult> WriteSingleAsync(string path, TagChanges changes)
    {
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync([new TagWriteRequest(path, changes)], CancellationToken.None);

        return Assert.Single(results);
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

    private static string[] FrameIds(string path)
    {
        return [.. ReadId3v2(path).GetFrames().Select(f => f.FrameId.ToString())];
    }

    private static List<string> RenderFramesExcept(string path, string excludedId)
    {
        TagLib.Id3v2.Tag tag = ReadId3v2(path);

        return [.. tag.GetFrames()
            .Where(f => f.FrameId.ToString() != excludedId)
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