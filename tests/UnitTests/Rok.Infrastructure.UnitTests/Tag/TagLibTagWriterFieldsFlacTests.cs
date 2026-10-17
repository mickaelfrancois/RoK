using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterFieldsFlacTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterFieldsFlacTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterFieldsFlacTests()
    {
        _sut = new TagLibTagWriter(JournalFolder, NullLogger<TagLibTagWriter>.Instance, _files);
    }

    private string JournalFolder => Path.Combine(_tempDir.FullName, "journal");

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [Theory(DisplayName = "flac_write_should_set_track_number_and_keep_the_track_total")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_ShouldSetTrackNumberAndKeepTotal(bool combinedTrack)
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 7 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        Assert.Equal(7u, file.Tag.Track);
        Assert.Equal(FlacTagFixture.OriginalTrackCount, file.Tag.TrackCount);
        Assert.Equal(7, result.Values!.TrackNumber);
    }

    [Fact(DisplayName = "flac_write_should_keep_the_total_of_a_combined_tracknumber")]
    public async Task WriteAsync_ShouldKeepTotalOfCombinedTrackNumber()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 5 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        Assert.Equal(12u, file.Tag.TrackCount);
        Assert.Equal(5u, file.Tag.Track);
    }

    [Fact(DisplayName = "flac_write_should_set_disc_and_keep_the_disc_total")]
    public async Task WriteAsync_ShouldSetDiscAndKeepTotal()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Disc = 2 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        Assert.Equal(2u, file.Tag.Disc);
        Assert.Equal(FlacTagFixture.OriginalDiscCount, file.Tag.DiscCount);
        Assert.Equal(2, result.Values!.Disc);
    }

    [Fact(DisplayName = "flac_write_should_set_composer")]
    public async Task WriteAsync_ShouldSetComposer()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "New composer" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(["New composer"], Field(path, "COMPOSER"));
        Assert.Equal("New composer", result.Values!.Composers);
    }

    [Fact(DisplayName = "flac_write_should_clear_composer")]
    public async Task WriteAsync_ShouldClearComposer()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Empty(Field(path, "COMPOSER"));
        Assert.Equal("", result.Values!.Composers);
    }

    [Fact(DisplayName = "flac_write_should_set_bpm")]
    public async Task WriteAsync_ShouldSetBpm()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 98 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(["98"], Field(path, "BPM"));
        Assert.Empty(Field(path, "TEMPO"));
        Assert.Equal(98, result.Values!.Bpm);
    }

    [Fact(DisplayName = "flac_write_should_update_tempo_when_present_and_write_bpm")]
    public async Task WriteAsync_ShouldUpdateTempoWhenPresent_AndWriteBpm()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false, withTempo: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 98 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(["98"], Field(path, "BPM"));
        Assert.Equal(["98"], Field(path, "TEMPO"));
        Assert.Equal(98, result.Values!.Bpm);
    }

    [Fact(DisplayName = "flac_write_should_clear_bpm")]
    public async Task WriteAsync_ShouldClearBpm()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false, withTempo: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 0 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Empty(Field(path, "BPM"));
        Assert.Empty(Field(path, "TEMPO"));
        Assert.Null(result.Values!.Bpm);
    }

    [Fact(DisplayName = "flac_write_should_keep_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);
        string hashBefore = HashFrames(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 9 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashFrames(path));
        Assert.Equal([FlacTagFixture.OriginalTitle], Field(path, "TITLE"));
        Assert.Equal([FlacTagFixture.OriginalComposer], Field(path, "COMPOSER"));
        Assert.Equal([FlacTagFixture.OriginalBpm.ToString()], Field(path, "BPM"));
        Assert.Equal([FlacTagFixture.OriginalDisc.ToString()], Field(path, "DISCNUMBER"));
        Assert.Equal([FlacTagFixture.LyricsText], Field(path, "LYRICS"));
        Assert.Equal([FlacTagFixture.MusicBrainzValue], Field(path, "MUSICBRAINZ_ALBUMID"));
    }

    [Fact(DisplayName = "flac_write_should_apply_all_fields_in_one_write")]
    public async Task WriteAsync_ShouldApplyAllFields_InOneWrite()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: true, withTempo: true);
        TagChanges changes = new() { Title = "Title", TrackNumber = 4, Disc = 2, Composers = "Someone", Bpm = 133 };

        // Act
        TagWriteResult result = await WriteSingleAsync(path, changes);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues("Title", 4, 2, "Someone", 133), result.Values);
    }

    [Fact(DisplayName = "flac_write_should_apply_changes_to_a_parasitic_id3v2")]
    public async Task WriteAsync_ShouldApplyChanges_ToAParasiticId3v2()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithParasiticId3v2(MusicFolder, 3);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 6, Composers = "Someone", Bpm = 101 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        TagLib.Id3v2.Tag id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, false);
        Assert.NotNull(id3v2);
        Assert.Equal(6u, id3v2.Track);
        Assert.Equal("Someone", id3v2.JoinedComposers);
        Assert.Equal(101u, id3v2.BeatsPerMinute);
        Assert.Equal(FlacTagFixture.OriginalArtist, id3v2.FirstPerformer);
        Assert.NotNull(TagLib.Id3v2.UserTextInformationFrame.Get(id3v2, "MusicBrainz Album Id", false));
    }

    [Fact(DisplayName = "flac_write_result_values_match_the_file_read_by_the_import")]
    public async Task WriteAsync_ShouldReturnValues_MatchingTheImportRead()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: true, withTempo: true);
        TagService service = new(NullLogger<TagService>.Instance);
        TrackFile track = new();

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Title = "T", TrackNumber = 6, Disc = 3, Composers = "A; B", Bpm = 140 });
        service.FillMusicProperties(path, track);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues(track.Title, track.TrackNumber ?? 0, track.Disc, track.Composers, track.Bpm), result.Values);
    }

    [Fact(DisplayName = "write_should_fail_and_keep_the_original_when_a_changed_field_does_not_read_back")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenChangedFieldDoesNotReadBack()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithNumbers(MusicFolder, combinedTrack: false);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.AfterWrite = copy => AlterBpm(copy, "1");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 99 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.VerificationFailed, result.Reason);
        Assert.Equal(SHA256.HashData(before), SHA256.HashData(await File.ReadAllBytesAsync(path)));
    }

    [Fact(DisplayName = "flac_write_should_set_then_remove_the_compilation_flag")]
    public async Task WriteAsync_ShouldSetThenRemoveCompilationFlag()
    {
        // Arrange
        string path = FlacTagFixture.CreateRich(MusicFolder);

        // Act
        TagWriteResult set = await WriteSingleAsync(path, new TagChanges { IsCompilation = true });
        bool afterSet = ReadFlag(path);
        TagWriteResult removed = await WriteSingleAsync(path, new TagChanges { IsCompilation = false });
        bool afterRemove = ReadFlag(path);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, set.Outcome);
        Assert.True(afterSet);
        Assert.True(set.Values!.IsCompilation);
        Assert.Equal(ETagWriteOutcome.Succeeded, removed.Outcome);
        Assert.False(afterRemove);
        Assert.False(removed.Values!.IsCompilation);
        Assert.Empty(Field(path, "COMPILATION"));
    }

    [Fact(DisplayName = "flac_write_should_remove_the_compilation_flag_from_a_parasitic_id3v2_too")]
    public async Task WriteAsync_ShouldRemoveCompilationFlag_FromAParasiticId3v2Too()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithParasiticId3v2(MusicFolder, 3);

        using (TagLib.File file = OpenFlac(path))
        {
            ((TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, false)).IsCompilation = true;
            ((TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true)).IsCompilation = true;
            file.Save();
        }

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { IsCompilation = false });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File reopened = OpenFlac(path);
        Assert.False(((TagLib.Id3v2.Tag)reopened.GetTag(TagLib.TagTypes.Id3v2, false)).IsCompilation);
        Assert.False(((TagLib.Ogg.XiphComment)reopened.GetTag(TagLib.TagTypes.Xiph, false)).IsCompilation);
        Assert.False(TagFieldReader.ReadCompilationFlag(reopened));
    }

    private static bool ReadFlag(string path)
    {
        using TagLib.File file = OpenFlac(path);

        return TagFieldReader.ReadCompilationFlag(file);
    }

    private async Task<TagWriteResult> WriteSingleAsync(string path, TagChanges changes)
    {
        IReadOnlyList<TagWriteResult> results = await _sut.WriteAsync([new TagWriteRequest(path, changes)], CancellationToken.None);

        return Assert.Single(results);
    }

    private static TagLib.File OpenFlac(string path)
    {
        return TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average);
    }

    private static string[] Field(string path, string name)
    {
        using TagLib.File file = OpenFlac(path);

        return ((TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, false)).GetField(name);
    }

    private static void AlterBpm(string path, string value)
    {
        using TagLib.File file = OpenFlac(path);

        ((TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true)).SetField("BPM", value);
        file.Save();
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
}