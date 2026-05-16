using System.Text;
using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists.Formats;

namespace Rok.Infrastructure.UnitTests.Playlists.Formats;

public class M3u8PlaylistWriterTests
{
    private static async Task<(string Text, byte[] Bytes)> WriteAsync(PlaylistFileModel model)
    {
        M3u8PlaylistWriter writer = new();
        using MemoryStream stream = new();
        await writer.WriteAsync(stream, model, CancellationToken.None);
        byte[] bytes = stream.ToArray();
        return (Encoding.UTF8.GetString(bytes), bytes);
    }

    [Fact(DisplayName = "writes_extm3u_header_first")]
    public async Task Writes_extm3u_header_first()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>());

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.StartsWith("#EXTM3U\n", text);
    }

    [Fact(DisplayName = "writes_extinf_with_seconds_and_artist_dash_title")]
    public async Task Writes_extinf_with_seconds_and_artist_dash_title()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "One More Time", "Daft Punk", TimeSpan.FromSeconds(215))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:215,Daft Punk - One More Time\n", text);
    }

    [Fact(DisplayName = "writes_path_after_extinf")]
    public async Task Writes_path_after_extinf()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", TimeSpan.FromSeconds(10))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:10,A - T\nD:\\Music\\track.mp3\n", text);
    }

    [Fact(DisplayName = "writes_extinf_with_minus_one_when_duration_unknown")]
    public async Task Writes_extinf_with_minus_one_when_duration_unknown()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", null)
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:-1,A - T\n", text);
    }

    [Fact(DisplayName = "output_is_utf8_without_bom")]
    public async Task Output_is_utf8_without_bom()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>());

        // Act
        (_, byte[] bytes) = await WriteAsync(model);

        // Assert — UTF-8 BOM is EF BB BF
        Assert.True(bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));
    }

    [Fact(DisplayName = "uses_lf_line_endings")]
    public async Task Uses_lf_line_endings()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "T", "A", TimeSpan.FromSeconds(10))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact(DisplayName = "emits_empty_artist_with_dash_when_artist_null")]
    public async Task Emits_empty_artist_with_dash_when_artist_null()
    {
        // Arrange
        PlaylistFileModel model = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\track.mp3", "Title only", null, TimeSpan.FromSeconds(60))
        });

        // Act
        (string text, _) = await WriteAsync(model);

        // Assert
        Assert.Contains("#EXTINF:60, - Title only\n", text);
    }

    [Fact(DisplayName = "roundtrip_with_reader_preserves_paths_and_metadata")]
    public async Task Roundtrip_with_reader_preserves_paths_and_metadata()
    {
        // Arrange
        PlaylistFileModel original = new("Mix", new List<PlaylistFileEntry>
        {
            new(@"D:\Music\a.mp3", "Title A", "Artist A", TimeSpan.FromSeconds(123)),
            new(@"D:\Music\b.mp3", "Title B - Live", "Artist B", TimeSpan.FromSeconds(45))
        });

        M3u8PlaylistWriter writer = new();
        using MemoryStream stream = new();
        await writer.WriteAsync(stream, original, CancellationToken.None);

        // Act
        stream.Position = 0;
        Rok.Infrastructure.Playlists.Formats.M3u8PlaylistReader reader = new();
        PlaylistFileModel parsed = await reader.ReadAsync(stream, "round.m3u8", CancellationToken.None);

        // Assert
        Assert.Equal(2, parsed.Entries.Count);
        Assert.Equal(@"D:\Music\a.mp3", parsed.Entries[0].FilePath);
        Assert.Equal("Title A", parsed.Entries[0].Title);
        Assert.Equal("Artist A", parsed.Entries[0].Artist);
        Assert.Equal(TimeSpan.FromSeconds(123), parsed.Entries[0].Duration);
        Assert.Equal("Title B - Live", parsed.Entries[1].Title);
    }
}