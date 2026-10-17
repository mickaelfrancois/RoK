using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterAlbumFieldsFlacTests : IDisposable
{
    private const string OldAlbum = "Old Album";
    private const string NewAlbum = "New Album";
    private const string FullDate = "2001-05-12";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterAlbumFieldsFlacTests_");
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterAlbumFieldsFlacTests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, new FaultInjectingFileOperations());
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [Fact(DisplayName = "flac_write_album_replaces_the_album_and_keeps_every_other_field")]
    public async Task WriteAsync_ShouldReplaceTheAlbumAndKeepEveryOtherField()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, OldAlbum, ["Old", "Jazz"], FullDate);
        string[] fieldsBefore = OtherFields(path, "ALBUM");
        string[] blocksBefore = OtherBlocks(path);
        string audioBefore = HashAudio(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(fieldsBefore, OtherFields(path, "ALBUM"));
        Assert.Equal(blocksBefore, OtherBlocks(path));
        Assert.Equal(audioBefore, HashAudio(path));
        Assert.Equal(NewAlbum, ReadXiph(path).Album);
        Assert.Equal(NewAlbum, result.Values!.Album);
        Assert.Equal("", result.Values.Genre);
        Assert.Null(result.Values.Year);
    }

    [Fact(DisplayName = "flac_write_genre_replaces_the_first_genre_and_keeps_the_others")]
    public async Task WriteAsync_ShouldReplaceTheFirstGenreAndKeepTheOthers()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, OldAlbum, ["Old", "Jazz"], FullDate);
        string[] fieldsBefore = OtherFields(path, "GENRE");
        string audioBefore = HashAudio(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Genre = "New" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(fieldsBefore, OtherFields(path, "GENRE"));
        Assert.Equal(audioBefore, HashAudio(path));
        Assert.Equal(["New", "Jazz"], ReadXiph(path).Genres);
        Assert.Equal("New", result.Values!.Genre);
        Assert.Equal("", result.Values.Album);
    }

    [Fact(DisplayName = "flac_write_year_replaces_the_year_and_keeps_every_other_field")]
    public async Task WriteAsync_ShouldReplaceTheYearAndKeepEveryOtherField()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, OldAlbum, ["Old"], "1999");
        string[] fieldsBefore = OtherFields(path, "DATE", "YEAR");
        string audioBefore = HashAudio(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2005 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(fieldsBefore, OtherFields(path, "DATE", "YEAR"));
        Assert.Equal(audioBefore, HashAudio(path));
        Assert.Equal(2005u, ReadXiph(path).Year);
        Assert.Equal(2005, result.Values!.Year);
        Assert.Equal("", result.Values.Album);
    }

    [Fact(DisplayName = "flac_write_year_replaces_a_full_date_by_the_year")]
    public async Task WriteAsync_ShouldReplaceAFullDateByTheYear()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, OldAlbum, ["Old"], FullDate);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2001 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(["2001"], ReadXiph(path).GetField("DATE"));
        Assert.Equal(2001, result.Values!.Year);
    }

    [Fact(DisplayName = "flac_write_album_fields_are_read_back_like_the_import")]
    public async Task WriteAsync_ShouldReadBackAlbumFieldsLikeTheImport()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, OldAlbum, ["Old"], "1999");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum, Genre = "Fusion", Year = 2010 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagFieldValues read = TagFieldReader.ReadFile(path, "taglib/flac");
        Assert.Equal(read.Album, result.Values!.Album);
        Assert.Equal(read.Genre, result.Values.Genre);
        Assert.Equal(read.Year, result.Values.Year);
        Assert.Equal(2010, result.Values.Year);
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

    private static TagLib.Ogg.XiphComment ReadXiph(string path)
    {
        using TagLib.File file = OpenFlac(path);

        return (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, false);
    }

    private static string[] OtherFields(string path, params string[] excludedNames)
    {
        return [.. FlacLayout.Read(path).VorbisFields
            .Select(f => f.Contains('=') ? string.Concat(f[..f.IndexOf('=')].ToUpperInvariant(), f.AsSpan(f.IndexOf('='))) : f.ToUpperInvariant())
            .Where(f => !excludedNames.Any(n => f.StartsWith(n + "=", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];
    }

    private static string[] OtherBlocks(string path)
    {
        return [.. FlacLayout.Read(path).Blocks
            .Where(b => b.Type is not (FlacLayout.VorbisCommentType or FlacLayout.PaddingType))
            .Select(b => $"{b.Type}:{Convert.ToHexString(SHA256.HashData(b.Data))}")];
    }

    private static string HashAudio(string path)
    {
        long start = FlacLayout.Read(path).FirstFrameOffset;
        byte[] bytes = File.ReadAllBytes(path);

        return Convert.ToHexString(SHA256.HashData(bytes.AsSpan((int)start)));
    }
}