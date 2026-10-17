using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterAlbumFieldsM4aTests : IDisposable
{
    private const string OldAlbum = "Old Album";
    private const string NewAlbum = "New Album";
    private const string FullDate = "2001-05-12";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterAlbumFieldsM4aTests_");
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterAlbumFieldsM4aTests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, new FaultInjectingFileOperations());
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_album_replaces_the_album_and_keeps_every_other_field")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public async Task WriteAsync_ShouldReplaceTheAlbumAndKeepEveryOtherField(EMp4Layout layout)
    {
        // Arrange
        string path = M4aTagFixture.CreateWithAlbumFields(MusicFolder, layout, OldAlbum, ["Old", "Jazz"], FullDate);
        string[] atomsBefore = OtherAtoms(path, "©alb");
        string mediaBefore = Mp4Layout.HashMediaBoxes(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(atomsBefore, OtherAtoms(path, "©alb"));
        Assert.Equal(mediaBefore, Mp4Layout.HashMediaBoxes(path));

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(NewAlbum, file.Tag.Album);
        Assert.Equal(NewAlbum, result.Values!.Album);
        Assert.Equal("", result.Values.Genre);
        Assert.Null(result.Values.Year);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_genre_replaces_the_first_genre_and_keeps_the_others")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public async Task WriteAsync_ShouldReplaceTheFirstGenreAndKeepTheOthers(EMp4Layout layout)
    {
        // Arrange
        string path = M4aTagFixture.CreateWithAlbumFields(MusicFolder, layout, OldAlbum, ["Old", "Jazz"], FullDate);
        string[] atomsBefore = OtherAtoms(path, "©gen", "gnre");
        string mediaBefore = Mp4Layout.HashMediaBoxes(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Genre = "New" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(atomsBefore, OtherAtoms(path, "©gen", "gnre"));
        Assert.Equal(mediaBefore, Mp4Layout.HashMediaBoxes(path));

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(["New", "Jazz"], file.Tag.Genres);
        Assert.Equal("New", result.Values!.Genre);
        Assert.Equal("", result.Values.Album);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_year_replaces_the_year_and_keeps_every_other_field")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public async Task WriteAsync_ShouldReplaceTheYearAndKeepEveryOtherField(EMp4Layout layout)
    {
        // Arrange
        string path = M4aTagFixture.CreateWithAlbumFields(MusicFolder, layout, OldAlbum, ["Old"], "1999");
        string[] atomsBefore = OtherAtoms(path, "©day");
        string mediaBefore = Mp4Layout.HashMediaBoxes(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2005 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(atomsBefore, OtherAtoms(path, "©day"));
        Assert.Equal(mediaBefore, Mp4Layout.HashMediaBoxes(path));

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(2005u, file.Tag.Year);
        Assert.Equal(2005, result.Values!.Year);
        Assert.Equal("", result.Values.Album);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_year_replaces_a_full_date_by_the_year")]
    public async Task WriteAsync_ShouldReplaceAFullDateByTheYear()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithAlbumFields(MusicFolder, EMp4Layout.MoovBeforeMdat, OldAlbum, ["Old"], FullDate);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Year = 2001 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(2001, result.Values!.Year);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(2001u, file.Tag.Year);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_album_fields_are_read_back_like_the_import")]
    public async Task WriteAsync_ShouldReadBackAlbumFieldsLikeTheImport()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithAlbumFields(MusicFolder, EMp4Layout.MoovBeforeMdat, OldAlbum, ["Old"], "1999");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Album = NewAlbum, Genre = "Fusion", Year = 2010 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagFieldValues read = TagFieldReader.ReadFile(path, "taglib/m4a");
        Assert.Equal(read.Album, result.Values!.Album);
        Assert.Equal(read.Genre, result.Values.Genre);
        Assert.Equal(read.Year, result.Values.Year);
        Assert.Equal("Fusion", result.Values.Genre);
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

    private static string[] OtherAtoms(string path, params string[] excludedTypes)
    {
        return [.. Mp4Layout.ReadIlstItems(path)
            .Where(i => !excludedTypes.Contains(i.Type))
            .Select(i => $"{i.Type}:{Convert.ToHexString(SHA256.HashData(i.Data))}")
            .Order(StringComparer.Ordinal)];
    }
}