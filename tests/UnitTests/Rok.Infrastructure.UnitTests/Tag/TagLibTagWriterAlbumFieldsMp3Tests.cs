using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterAlbumFieldsMp3Tests : IDisposable
{
    private const string OldAlbum = "Old Album";
    private const string NewAlbum = "New Album";
    private const string FullDate = "2001-05-12";

    private static readonly string[] YearFrames = ["TDRC", "TYER", "TDAT", "TIME"];

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterAlbumFieldsMp3Tests_");
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterAlbumFieldsMp3Tests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, new FaultInjectingFileOperations());
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_album_replaces_the_album_and_keeps_every_other_field")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldReplaceTheAlbumAndKeepEveryOtherField(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, version, OldAlbum, ["Old", "Jazz"], FullDate);
        string hashBefore = HashAudio(path);
        List<string> framesBefore = RenderFramesExcept(path, "TALB");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
        Assert.Equal(framesBefore, RenderFramesExcept(path, "TALB"));
        Assert.Equal(NewAlbum, ReadId3v2(path).Album);
        Assert.Equal(NewAlbum, result.Values!.Album);
        Assert.Equal("", result.Values.Genre);
        Assert.Null(result.Values.Year);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_genre_replaces_the_first_genre_and_keeps_the_others")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldReplaceTheFirstGenreAndKeepTheOthers(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, version, OldAlbum, ["Old", "Jazz"], FullDate);
        string hashBefore = HashAudio(path);
        List<string> framesBefore = RenderFramesExcept(path, "TCON");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Genre = "New" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
        Assert.Equal(framesBefore, RenderFramesExcept(path, "TCON"));
        Assert.Equal(["New", "Jazz"], ReadId3v2(path).Genres);
        Assert.Equal("New", result.Values!.Genre);
        Assert.Equal("", result.Values.Album);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_year_replaces_the_year_and_keeps_every_other_field")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldReplaceTheYearAndKeepEveryOtherField(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, version, OldAlbum, ["Old"], "1999");
        string hashBefore = HashAudio(path);
        List<string> framesBefore = RenderFramesExcept(path, YearFrames);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2005 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
        Assert.Equal(framesBefore, RenderFramesExcept(path, YearFrames));
        Assert.Equal(2005u, ReadId3v2(path).Year);
        Assert.Equal(2005, result.Values!.Year);
        Assert.Equal("", result.Values.Album);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_year_replaces_a_full_date_by_the_year")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldReplaceAFullDateByTheYear(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, version, OldAlbum, ["Old"], FullDate);
        List<string> framesBefore = RenderFramesExcept(path, YearFrames);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2001 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(framesBefore, RenderFramesExcept(path, YearFrames));
        Assert.Equal(2001u, ReadId3v2(path).Year);
        Assert.Equal(2001, result.Values!.Year);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_genre_syncs_existing_id3v1_and_ape")]
    public async Task WriteAsync_ShouldSyncExistingId3v1AndApe_WhenWritingAlbumFields()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, 3, OldAlbum, ["Rock"], "1999", withId3v1: true, withApe: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum, Genre = "Blues", Year = 2005 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        Assert.Equal("Blues", file.Tag.FirstGenre);
        Assert.Equal(NewAlbum, file.Tag.Album);
        Assert.Equal(2005u, file.Tag.Year);

        TagLib.Tag id3v1 = file.GetTag(TagLib.TagTypes.Id3v1, false);
        Assert.Equal("Blues", id3v1.FirstGenre);
        Assert.Equal(NewAlbum, id3v1.Album);
        Assert.Equal(2005u, id3v1.Year);

        TagLib.Tag ape = file.GetTag(TagLib.TagTypes.Ape, false);
        Assert.Equal("Blues", ape.FirstGenre);
        Assert.Equal(NewAlbum, ape.Album);
        Assert.Equal(2005u, ape.Year);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_free_genre_outside_the_id3v1_list_is_kept_in_id3v2")]
    public async Task WriteAsync_ShouldKeepAFreeGenreInId3v2_WhenItIsOutsideTheId3v1List()
    {
        // Arrange
        const string freeGenre = "Zouk Retro Fusion";
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, 3, OldAlbum, ["Rock"], "1999", withId3v1: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Genre = freeGenre });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal([freeGenre], ReadId3v2(path).Genres);
        Assert.Equal(freeGenre, result.Values!.Genre);
        Assert.Equal(freeGenre, TagFieldReader.ReadFile(path, "taglib/mp3").Genre);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_album_fields_are_read_back_like_the_import")]
    public async Task WriteAsync_ShouldReadBackAlbumFieldsLikeTheImport()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithAlbumFields(MusicFolder, 4, OldAlbum, ["Old"], "1999");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = "  Spaced ‐ Album ", Genre = " Fusion ", Year = 2010 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagFieldValues read = TagFieldReader.ReadFile(path, "taglib/mp3");
        Assert.Equal("Spaced - Album", result.Values!.Album);
        Assert.Equal("Fusion", result.Values.Genre);
        Assert.Equal(2010, result.Values.Year);
        Assert.Equal(read.Album, result.Values.Album);
        Assert.Equal(read.Genre, result.Values.Genre);
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

    private static List<string> RenderFramesExcept(string path, params string[] excludedIds)
    {
        TagLib.Id3v2.Tag tag = ReadId3v2(path);

        return [.. tag.GetFrames()
            .Where(f => !excludedIds.Contains(f.FrameId.ToString()))
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
}