using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Tag.Formats;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterArtistFlacTests : IDisposable
{
    private const string OldArtist = "Old Artist";
    private const string NewArtist = "New Artist";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterArtistFlacTests_");
    private readonly FaultInjectingFileOperations _files = new();
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterArtistFlacTests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, _files);
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [Fact(DisplayName = "flac_write_artist_replaces_the_album_artist_when_present")]
    public async Task WriteAsync_ShouldReplaceTheAlbumArtist_WhenPresent()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, [OldArtist], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Ogg.XiphComment xiph = ReadXiph(path);
        Assert.Equal([NewArtist], xiph.AlbumArtists);
        Assert.Equal([OldArtist], xiph.Performers);
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [Fact(DisplayName = "flac_write_artist_replaces_the_performer_without_album_artist")]
    public async Task WriteAsync_ShouldReplaceThePerformer_WithoutAlbumArtist()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, [], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Ogg.XiphComment xiph = ReadXiph(path);
        Assert.Equal([NewArtist], xiph.Performers);
        Assert.Empty(xiph.AlbumArtists);
        Assert.Empty(xiph.GetField("ALBUMARTIST"));
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [Theory(DisplayName = "flac_write_artist_keeps_other_values_of_a_multi_valued_field")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_ShouldKeepTheOtherValues_OfAMultiValuedField(bool withAlbumArtist)
    {
        // Arrange
        string[] albumArtists = withAlbumArtist ? [OldArtist, "Guest"] : [];
        string[] performers = withAlbumArtist ? ["Someone"] : [OldArtist, "Guest"];
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, albumArtists, performers);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Ogg.XiphComment xiph = ReadXiph(path);

        if (withAlbumArtist)
        {
            Assert.Equal([NewArtist, "Guest"], xiph.AlbumArtists);
            Assert.Equal(["Someone"], xiph.Performers);
        }
        else
        {
            Assert.Equal([NewArtist, "Guest"], xiph.Performers);
        }
    }

    [Fact(DisplayName = "flac_write_artist_keeps_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio_WhenWritingTheArtist()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, [OldArtist], [OldArtist]);
        string audioBefore = HashFrames(path);
        string[] fieldsBefore = OtherFields(path);
        string[] blocksBefore = OtherBlocks(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(audioBefore, HashFrames(path));
        Assert.Equal(fieldsBefore, OtherFields(path));
        Assert.Equal(blocksBefore, OtherBlocks(path));

        TagLib.Ogg.XiphComment xiph = ReadXiph(path);
        Assert.Equal(FlacTagFixture.OriginalTitle, xiph.Title);
    }

    [Fact(DisplayName = "flac_write_artist_updates_a_parasitic_id3v2_tag")]
    public async Task WriteAsync_ShouldUpdateAParasiticId3v2Tag_WhenWritingTheArtist()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithParasiticId3v2(MusicFolder, 4);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenFlac(path);
        Assert.Equal(NewArtist, file.Tag.FirstPerformer);
        Assert.Equal(NewArtist, file.GetTag(TagLib.TagTypes.Id3v2, false).FirstPerformer);
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [Fact(DisplayName = "flac_write_artist_replaces_the_spaced_album_artist_field")]
    public async Task WriteAsync_ShouldReplaceTheSpacedAlbumArtistField()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, [], [OldArtist]);

        using (TagLib.File file = OpenFlac(path))
        {
            ((TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true)).SetField("ALBUM ARTIST", OldArtist);
            file.Save();
        }

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File reopened = OpenFlac(path);
        Assert.Equal(NewArtist, reopened.Tag.FirstAlbumArtist);
        Assert.Equal([OldArtist], reopened.Tag.Performers);
    }

    [Fact(DisplayName = "flac_write_artist_fails_and_keeps_the_original_when_the_artist_does_not_read_back")]
    public async Task WriteAsync_ShouldFailAndKeepOriginal_WhenTheArtistDoesNotReadBack()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithArtists(MusicFolder, [OldArtist], [OldArtist]);
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

    private static TagLib.File OpenFlac(string path)
    {
        return TagLib.File.Create(path, "taglib/flac", TagLib.ReadStyle.Average);
    }

    private static TagLib.Ogg.XiphComment ReadXiph(string path)
    {
        using TagLib.File file = OpenFlac(path);

        return (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, false);
    }

    private static void AlterAlbumArtist(string path, string value)
    {
        using TagLib.File file = OpenFlac(path);

        ((TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true)).AlbumArtists = [value];
        file.Save();
    }

    private static string[] OtherFields(string path)
    {
        return [.. FlacLayout.Read(path).VorbisFields
            .Select(f => f.Contains('=') ? string.Concat(f[..f.IndexOf('=')].ToUpperInvariant(), f.AsSpan(f.IndexOf('='))) : f.ToUpperInvariant())
            .Where(f => !f.StartsWith("ARTIST=", StringComparison.Ordinal)
                && !f.StartsWith("ALBUMARTIST=", StringComparison.Ordinal)
                && !f.StartsWith("ALBUM ARTIST=", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    private static string[] OtherBlocks(string path)
    {
        return [.. FlacLayout.Read(path).Blocks
            .Where(b => b.Type is not (FlacLayout.VorbisCommentType or FlacLayout.PaddingType))
            .Select(b => $"{b.Type}:{Convert.ToHexString(SHA256.HashData(b.Data))}")];
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