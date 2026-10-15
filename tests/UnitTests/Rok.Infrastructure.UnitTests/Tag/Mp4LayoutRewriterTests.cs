using System.Security.Cryptography;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Rok.Infrastructure.UnitTests.TestData;

namespace Rok.Infrastructure.UnitTests.Tag;

public sealed class Mp4LayoutRewriterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Mp4LayoutRewriterTests_" + Guid.NewGuid().ToString("N"));

    public Mp4LayoutRewriterTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [MediaFoundationEncoderTheory(AudioFixtureFormat.M4a, DisplayName = "m4a_layout_rewriter_should_move_moov_and_keep_the_decoded_pcm_identical")]
    [InlineData(EMp4Layout.MoovBeforeMdat)]
    [InlineData(EMp4Layout.MoovAfterMdat)]
    public void Rewrite_should_move_moov_and_keep_the_decoded_pcm_identical(EMp4Layout target)
    {
        // Arrange
        EMp4Layout other = target == EMp4Layout.MoovBeforeMdat ? EMp4Layout.MoovAfterMdat : EMp4Layout.MoovBeforeMdat;
        string path = M4aTagFixture.CreateWithoutTags(_folder, other);
        string expectedHash = DecodeHash(path);

        // Act
        Mp4LayoutRewriter.Rewrite(path, target);

        // Assert
        Assert.Equal(target, Mp4LayoutRewriter.GetLayout(path));
        Assert.Equal(expectedHash, DecodeHash(path));
    }

    [MediaFoundationEncoderFact(AudioFixtureFormat.M4a, DisplayName = "m4a_layout_rewriter_should_give_the_same_pcm_for_both_layouts")]
    public void Rewrite_should_give_the_same_pcm_for_both_layouts()
    {
        // Arrange
        string before = M4aTagFixture.CreateRich(_folder, EMp4Layout.MoovBeforeMdat, "before.m4a");
        string after = M4aTagFixture.CreateRich(_folder, EMp4Layout.MoovAfterMdat, "after.m4a");

        // Act
        string beforeHash = DecodeHash(before);
        string afterHash = DecodeHash(after);

        // Assert
        Assert.Equal(EMp4Layout.MoovBeforeMdat, Mp4LayoutRewriter.GetLayout(before));
        Assert.Equal(EMp4Layout.MoovAfterMdat, Mp4LayoutRewriter.GetLayout(after));
        Assert.Equal(beforeHash, afterHash);
    }

    private static string DecodeHash(string path)
    {
        MediaFoundationApi.Startup();

        using MediaFoundationReader reader = new(path);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        long total = 0;
        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            total += read;
        }

        Assert.True(total > 0, "Decoded PCM is empty.");

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}