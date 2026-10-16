using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterArtistM4aTests : IDisposable
{
    private const string OldArtist = "Old Artist";
    private const string NewArtist = "New Artist";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterArtistM4aTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterArtistM4aTests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, _files);
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_artist_replaces_the_album_artist_when_present")]
    public async Task WriteAsync_ShouldReplaceTheAlbumArtist_WhenPresent()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithArtists(MusicFolder, EMp4Layout.MoovAfterMdat, [OldArtist], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal([NewArtist], file.Tag.AlbumArtists);
        Assert.Equal([OldArtist], file.Tag.Performers);
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_artist_replaces_the_performer_without_album_artist")]
    public async Task WriteAsync_ShouldReplaceThePerformer_WithoutAlbumArtist()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithArtists(MusicFolder, EMp4Layout.MoovBeforeMdat, [], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);
        Assert.Equal([NewArtist], file.Tag.Performers);
        Assert.Empty(file.Tag.AlbumArtists);
        Assert.DoesNotContain(Mp4Layout.ReadIlstItems(path), i => i.Type == "aART");
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_write_artist_keeps_other_values_of_a_multi_valued_field")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_ShouldKeepTheOtherValues_OfAMultiValuedField(bool withAlbumArtist)
    {
        // Arrange
        string[] albumArtists = withAlbumArtist ? [OldArtist, "Guest"] : [];
        string[] performers = withAlbumArtist ? ["Someone"] : [OldArtist, "Guest"];
        string path = M4aTagFixture.CreateWithArtists(MusicFolder, EMp4Layout.MoovAfterMdat, albumArtists, performers);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenM4a(path);

        if (withAlbumArtist)
        {
            Assert.Equal([NewArtist, "Guest"], file.Tag.AlbumArtists);
            Assert.Equal(["Someone"], file.Tag.Performers);
        }
        else
        {
            Assert.Equal([NewArtist, "Guest"], file.Tag.Performers);
        }
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_artist_keeps_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio_WhenWritingTheArtist()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithArtists(MusicFolder, EMp4Layout.MoovBeforeMdat, [OldArtist], [OldArtist]);
        string mediaBefore = Mp4Layout.HashMediaBoxes(path);
        string[] atomsBefore = OtherAtoms(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(mediaBefore, Mp4Layout.HashMediaBoxes(path));
        Assert.Equal(atomsBefore, OtherAtoms(path));

        using TagLib.File file = OpenM4a(path);
        Assert.Equal(M4aTagFixture.OriginalTitle, file.Tag.Title);
        Assert.Equal(M4aTagFixture.OriginalTrack, file.Tag.Track);
        Assert.Equal(M4aTagFixture.OriginalComposer, file.Tag.JoinedComposers);
        Assert.Equal(M4aTagFixture.OriginalBpm, file.Tag.BeatsPerMinute);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_write_artist_fails_and_keeps_the_original_when_the_artist_does_not_read_back")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenTheArtistDoesNotReadBack()
    {
        // Arrange
        string path = M4aTagFixture.CreateWithArtists(MusicFolder, EMp4Layout.MoovBeforeMdat, [OldArtist], [OldArtist]);
        byte[] before = await File.ReadAllBytesAsync(path);
        _files.AfterWrite = copy => AlterAlbumArtist(copy, "Other");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

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

    private static void AlterAlbumArtist(string path, string value)
    {
        using TagLib.File file = OpenM4a(path);

        file.GetTag(TagLib.TagTypes.Apple, true).AlbumArtists = [value];
        file.Save();
    }

    private static string[] OtherAtoms(string path)
    {
        return [.. Mp4Layout.ReadIlstItems(path)
            .Where(i => i.Type is not ("©ART" or "aART"))
            .Select(i => $"{i.Type}:{Convert.ToHexString(SHA256.HashData(i.Data))}")
            .Order(StringComparer.Ordinal)];
    }
}