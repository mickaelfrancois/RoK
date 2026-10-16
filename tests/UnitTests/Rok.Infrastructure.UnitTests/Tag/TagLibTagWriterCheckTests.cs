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
        Assert.Equal(new TagWriteCheck(writable, ETagWriteReason.None, FlacTagFixture.OriginalArtist), checks[0] with { FileDateModified = null, Size = null });
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

    private static Dictionary<string, (string Hash, DateTime Date)> Snapshot(params string[] paths)
    {
        return paths.ToDictionary(p => p, p => (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))), File.GetLastWriteTimeUtc(p)));
    }
}