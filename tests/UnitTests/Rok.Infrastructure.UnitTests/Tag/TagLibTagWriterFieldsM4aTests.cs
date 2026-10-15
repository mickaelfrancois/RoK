using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterFieldsM4aTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterFieldsM4aTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterFieldsM4aTests()
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

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_set_track_number_and_keep_the_track_total")]
    public async Task WriteAsync_ShouldSetTrackNumberAndKeepTotal()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 7 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(7u, file.Tag.Track);
        Assert.Equal(M4aTagFixture.OriginalTrackCount, file.Tag.TrackCount);
        Assert.Equal(7, result.Values!.TrackNumber);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_set_disc_and_keep_the_disc_total")]
    public async Task WriteAsync_ShouldSetDiscAndKeepTotal()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Disc = 2 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(2u, file.Tag.Disc);
        Assert.Equal(M4aTagFixture.OriginalDiscCount, file.Tag.DiscCount);
        Assert.Equal(2, result.Values!.Disc);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_set_composer")]
    public async Task WriteAsync_ShouldSetComposer()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "New composer" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal("New composer", file.Tag.JoinedComposers);
        Assert.Equal("New composer", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_clear_composer")]
    public async Task WriteAsync_ShouldClearComposer()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Composers = "" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(Mp4Layout.ReadIlstItems(path), i => i.Type == "©wrt");
        Assert.Equal("", result.Values!.Composers);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_set_bpm")]
    public async Task WriteAsync_ShouldSetBpm()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 98 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(98u, file.Tag.BeatsPerMinute);
        Assert.Equal(98, result.Values!.Bpm);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_clear_bpm")]
    public async Task WriteAsync_ShouldClearBpm()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 0 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(Mp4Layout.ReadIlstItems(path), i => i.Type == "tmpo");
        Assert.Null(result.Values!.Bpm);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_apply_all_fields_in_one_write")]
    public async Task WriteAsync_ShouldApplyAllFields_InOneWrite()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
        TagChanges changes = new() { Title = "Title", TrackNumber = 4, Disc = 2, Composers = "Someone", Bpm = 133 };

        // Act
        TagWriteResult result = await WriteSingleAsync(path, changes);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues("Title", 4, 2, "Someone", 133), result.Values);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_keep_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
        string mediaBefore = Mp4Layout.HashMediaBoxes(path);
        string offsetsBefore = Mp4Layout.HashRelativeChunkOffsets(path);
        IReadOnlyList<Mp4IlstItem> before = Mp4Layout.ReadIlstItems(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { TrackNumber = 9, Composers = new string('x', 5000) });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(mediaBefore, Mp4Layout.HashMediaBoxes(path));
        Assert.Equal(offsetsBefore, Mp4Layout.HashRelativeChunkOffsets(path));
        Assert.Equal(OtherAtoms(before, "trkn", "©wrt"), OtherAtoms(Mp4Layout.ReadIlstItems(path), "trkn", "©wrt"));

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(M4aTagFixture.OriginalTitle, file.Tag.Title);
        Assert.Equal(M4aTagFixture.OriginalDisc, file.Tag.Disc);
        Assert.Equal(M4aTagFixture.OriginalBpm, file.Tag.BeatsPerMinute);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_result_values_match_the_file_read_by_the_import")]
    public async Task WriteAsync_ShouldReturnValues_MatchingTheImportRead()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovAfterMdat);
        TagService service = new(NullLogger<TagService>.Instance);
        TrackFile track = new();

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Title = "T", TrackNumber = 6, Disc = 3, Composers = "A; B", Bpm = 140 });
        service.FillMusicProperties(path, track);

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(new TagFieldValues(track.Title, track.TrackNumber ?? 0, track.Disc, track.Composers, track.Bpm), result.Values);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_should_fail_and_keep_the_original_when_a_changed_field_does_not_read_back")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenChangedFieldDoesNotReadBack()
    {
        // Arrange
        string path = M4aTagFixture.CreateRich(MusicFolder, EMp4Layout.MoovBeforeMdat);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.AfterWrite = copy => AlterBpm(copy, 1);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 99 });

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

    private static TagLib.File OpenM4a(string path)
    {
        return TagLib.File.Create(path, "taglib/m4a", TagLib.ReadStyle.Average);
    }

    private static void AlterBpm(string path, uint value)
    {
        using TagLib.File file = OpenM4a(path);

        file.GetTag(TagLib.TagTypes.Apple, true).BeatsPerMinute = value;
        file.Save();
    }

    private static string[] OtherAtoms(IReadOnlyList<Mp4IlstItem> items, params string[] excluded)
    {
        return items
            .Where(i => i.Type != Mp4Layout.TitleAtomType && !excluded.Contains(i.Type))
            .Select(i => $"{i.Type}:{Convert.ToHexString(SHA256.HashData(i.Data))}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}