using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterArtistMp3Tests : IDisposable
{
    private const string OldArtist = "Old Artist";
    private const string NewArtist = "New Artist";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterArtistMp3Tests_");
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterArtistMp3Tests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, new FaultInjectingFileOperations());
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        _tempDir.Delete(recursive: true);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_replaces_the_album_artist_when_present")]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    public async Task WriteAsync_ShouldReplaceTheAlbumArtist_WhenPresent(byte version)
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, version, [OldArtist], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        Assert.Equal([NewArtist], tag.AlbumArtists);
        Assert.Equal([OldArtist], tag.Performers);
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_replaces_the_performer_without_album_artist")]
    public async Task WriteAsync_ShouldReplaceThePerformer_WithoutAlbumArtist()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 4, [], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        Assert.Equal([NewArtist], tag.Performers);
        Assert.Empty(tag.AlbumArtists);
        Assert.DoesNotContain("TPE2", FrameIds(path));
        Assert.Equal(NewArtist, result.Values!.Artist);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_keeps_other_values_of_a_multi_valued_field")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_ShouldKeepTheOtherValues_OfAMultiValuedField(bool withAlbumArtist)
    {
        // Arrange
        string[] albumArtists = withAlbumArtist ? [OldArtist, "Guest"] : [];
        string[] performers = withAlbumArtist ? ["Someone"] : [OldArtist, "Guest"];
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 4, albumArtists, performers);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        TagLib.Id3v2.Tag tag = ReadId3v2(path);

        if (withAlbumArtist)
        {
            Assert.Equal([NewArtist, "Guest"], tag.AlbumArtists);
            Assert.Equal(["Someone"], tag.Performers);
        }
        else
        {
            Assert.Equal([NewArtist, "Guest"], tag.Performers);
        }
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_keeps_every_other_field_and_the_audio")]
    public async Task WriteAsync_ShouldKeepEveryOtherFieldAndTheAudio_WhenWritingTheArtist()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 3, [OldArtist], [OldArtist]);
        string hashBefore = HashAudio(path);
        List<string> framesBefore = RenderFramesExcept(path, "TPE1", "TPE2");

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal(hashBefore, HashAudio(path));
        Assert.Equal(framesBefore, RenderFramesExcept(path, "TPE1", "TPE2"));

        TagLib.Id3v2.Tag tag = ReadId3v2(path);
        Assert.Equal(Mp3TagFixture.OriginalTitle, tag.Title);
        Assert.Equal(Mp3TagFixture.OriginalTrack, tag.Track);
        Assert.Equal(Mp3TagFixture.OriginalComposer, tag.JoinedComposers);
        Assert.Equal(Mp3TagFixture.OriginalBpm, tag.BeatsPerMinute);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_syncs_an_existing_ape_tag")]
    public async Task WriteAsync_ShouldSyncAnExistingApeTag_WhenWritingTheArtist()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 3, [], [OldArtist], withApe: true);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = NewArtist });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);

        using TagLib.File file = OpenMp3(path);
        TagLib.Tag ape = file.GetTag(TagLib.TagTypes.Ape, false);
        Assert.NotNull(ape);
        Assert.Equal(NewArtist, ape.FirstPerformer);
        Assert.Equal(NewArtist, file.Tag.FirstPerformer);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_write_artist_does_not_report_the_artist_when_it_is_not_changed")]
    public async Task WriteAsync_ShouldNotReportTheArtist_WhenItIsNotChanged()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 3, [OldArtist], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Bpm = 99 });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal("", result.Values!.Artist);
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_v23_artist_with_slash_is_rejected_and_the_original_stays_intact")]
    public async Task WriteAsync_ShouldRejectASlashArtist_InId3v23_AndKeepTheOriginal()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 3, [], [OldArtist]);
        byte[] before = await File.ReadAllBytesAsync(path);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = "AC/DC" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Failed, result.Outcome);
        Assert.Equal(ETagWriteReason.VerificationFailed, result.Reason);
        Assert.Equal(SHA256.HashData(before), SHA256.HashData(await File.ReadAllBytesAsync(path)));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "mp3_v24_artist_with_slash_is_written_and_read_back_identically")]
    public async Task WriteAsync_ShouldWriteASlashArtist_InId3v24()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 4, [], [OldArtist]);

        // Act
        TagWriteResult result = await WriteSingleAsync(path, new TagChanges { Artist = "AC/DC" });

        // Assert
        Assert.Equal(ETagWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal("AC/DC", result.Values!.Artist);
        Assert.Equal("AC/DC", ReadId3v2(path).FirstPerformer);
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