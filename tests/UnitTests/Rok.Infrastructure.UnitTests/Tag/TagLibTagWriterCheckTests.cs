using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Rok.Application.Tag;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class TagLibTagWriterCheckTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("TagLibTagWriterCheckTests_");
    private readonly TagLibTagWriter _sut;

    public TagLibTagWriterCheckTests()
    {
        _sut = new TagLibTagWriter(Path.Combine(_tempDir.FullName, "journal"), NullLogger<TagLibTagWriter>.Instance, new FaultInjectingFileOperations());
    }

    private string MusicFolder => Path.Combine(_tempDir.FullName, "music");

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_tempDir.FullName, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        _tempDir.Delete(recursive: true);
    }

    [Fact(DisplayName = "check_reports_reason_and_current_artist_without_writing")]
    public async Task CheckAsync_ShouldReportReasonAndCurrentArtist_WithoutWriting()
    {
        // Arrange
        string writable = FlacTagFixture.CreateRich(MusicFolder, "writable.flac");
        string readOnly = FlacTagFixture.CreateRich(MusicFolder, "readonly.flac");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);

        Directory.CreateDirectory(MusicFolder);
        string unsupported = Path.Combine(MusicFolder, "song.wma");
        await File.WriteAllBytesAsync(unsupported, [1, 2, 3]);

        string missing = Path.Combine(MusicFolder, "missing.flac");
        string[] paths = [writable, readOnly, unsupported, missing];
        Dictionary<string, (string Hash, DateTime Date)> before = Snapshot(writable, readOnly, unsupported);

        // Act
        IReadOnlyList<TagWriteCheck> checks = await _sut.CheckAsync(paths, CancellationToken.None);

        // Assert
        Assert.Equal(paths, checks.Select(c => c.FilePath));
        Assert.Equal(new TagWriteCheck(writable, ETagWriteReason.None, FlacTagFixture.OriginalArtist), checks[0] with { FileDateModified = null, Size = null, Current = null });
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(writable)), checks[0].FileDateModified);
        Assert.Equal(new FileInfo(writable).Length, checks[0].Size);
        Assert.Equal(new TagWriteCheck(readOnly, ETagWriteReason.ReadOnly, null), checks[1]);
        Assert.Equal(new TagWriteCheck(unsupported, ETagWriteReason.UnsupportedFormat, null), checks[2]);
        Assert.Equal(new TagWriteCheck(missing, ETagWriteReason.NotFound, null), checks[3]);
        Assert.Equal(before, Snapshot(writable, readOnly, unsupported));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.Mp3, DisplayName = "check_reads_the_artist_like_the_import_on_mp3")]
    public async Task CheckAsync_ShouldReadTheArtistLikeTheImport_OnMp3()
    {
        // Arrange
        string path = Mp3TagFixture.CreateWithArtists(MusicFolder, 4, ["  Album Artist "], ["Performer"]);

        // Act
        IReadOnlyList<TagWriteCheck> checks = await _sut.CheckAsync([path], CancellationToken.None);

        // Assert
        Assert.Equal("Album Artist", Assert.Single(checks).CurrentArtist);
    }

    [Fact(DisplayName = "check_returns_current_album_year_and_genre_like_the_import")]
    public async Task CheckAsync_ShouldReturnCurrentAlbumYearAndGenreLikeTheImport()
    {
        // Arrange
        string path = FlacTagFixture.CreateWithAlbumFields(MusicFolder, "  Spaced ‐ Album ", [" Rock‐Pop ", "Jazz"], "2001-05-12");
        TagService service = new(NullLogger<TagService>.Instance);
        TrackFile track = new();

        service.FillMusicProperties(path, track);

        // Act
        IReadOnlyList<TagWriteCheck> checks = await _sut.CheckAsync([path], CancellationToken.None);

        // Assert
        TagFieldValues current = Assert.Single(checks).Current!;
        Assert.Equal(track.Album, current.Album);
        Assert.Equal(track.Genre, current.Genre);
        Assert.Equal(track.Year, current.Year);
        Assert.Equal(track.Title, current.Title);
        Assert.Equal(track.Artist, current.Artist);
        Assert.Equal("Spaced - Album", current.Album);
        Assert.Equal("Rock-Pop", current.Genre);
        Assert.Equal(2001, current.Year);
    }

    [Fact(DisplayName = "check_returns_no_current_values_when_the_file_is_skipped")]
    public async Task CheckAsync_ShouldReturnNoCurrentValues_WhenTheFileIsSkipped()
    {
        // Arrange
        string missing = Path.Combine(MusicFolder, "missing.flac");

        // Act
        IReadOnlyList<TagWriteCheck> checks = await _sut.CheckAsync([missing], CancellationToken.None);

        // Assert
        Assert.Null(Assert.Single(checks).Current);
    }

    private static Dictionary<string, (string Hash, DateTime Date)> Snapshot(params string[] paths)
    {
        return paths.ToDictionary(p => p, p => (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))), File.GetLastWriteTimeUtc(p)));
    }
}