using Rok.Application.Features.Playlists.IO;
using Rok.Infrastructure.Playlists.Formats;

namespace Rok.Infrastructure.UnitTests.Playlists.Formats;

public class M3u8PlaylistReaderTests
{
    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TestData", "Playlists", name);

    private static async Task<PlaylistFileModel> ReadAsync(string fileName)
    {
        M3u8PlaylistReader reader = new();
        await using FileStream fs = File.OpenRead(FixturePath(fileName));
        return await reader.ReadAsync(fs, fileName, CancellationToken.None);
    }

    [Fact(DisplayName = "reads_minimal_playlist_with_paths_only")]
    public async Task Reads_minimal_playlist_with_paths_only()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("minimal.m3u8");

        // Assert
        Assert.Equal("minimal", model.Name);
        Assert.Equal(3, model.Entries.Count);
        Assert.Null(model.Entries[0].Artist);
        Assert.Null(model.Entries[0].Title);
        Assert.Null(model.Entries[0].Duration);
    }

    [Fact(DisplayName = "reads_extm3u_header_when_present")]
    public async Task Reads_extm3u_header_when_present()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("extended.m3u8");

        // Assert
        Assert.Equal(2, model.Entries.Count);
    }

    [Fact(DisplayName = "tolerates_missing_extm3u_header")]
    public async Task Tolerates_missing_extm3u_header()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("minimal.m3u8");

        // Assert — minimal.m3u8 has no #EXTM3U yet still parses
        Assert.NotEmpty(model.Entries);
    }

    [Fact(DisplayName = "reads_extinf_artist_title_and_duration")]
    public async Task Reads_extinf_artist_title_and_duration()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("extended.m3u8");

        // Assert
        PlaylistFileEntry first = model.Entries[0];
        Assert.Equal("Daft Punk", first.Artist);
        Assert.Equal("One More Time", first.Title);
        Assert.Equal(TimeSpan.FromSeconds(215), first.Duration);
    }

    [Fact(DisplayName = "parses_extinf_with_dash_in_title")]
    public async Task Parses_extinf_with_dash_in_title()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("with_dash_in_title.m3u8");

        // Assert — split on FIRST " - "
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Bowie", only.Artist);
        Assert.Equal("Ziggy Stardust - Live", only.Title);
    }

    [Fact(DisplayName = "treats_unknown_directives_as_comments")]
    public async Task Treats_unknown_directives_as_comments()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("unknown_directives.m3u8");

        // Assert — name comes from filename, not from #PLAYLIST
        Assert.Equal("unknown_directives", model.Name);
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Foo", only.Artist);
        Assert.Equal("Bar", only.Title);
    }

    [Fact(DisplayName = "skips_blank_lines")]
    public async Task Skips_blank_lines()
    {
        // Arrange — write a tmp file inline so we can interleave blanks
        string tmp = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmp, "#EXTM3U\n\n\nD:\\foo.mp3\n\n");
        try
        {
            M3u8PlaylistReader reader = new();
            await using FileStream fs = File.OpenRead(tmp);

            // Act
            PlaylistFileModel model = await reader.ReadAsync(fs, "tmp.m3u8", CancellationToken.None);

            // Assert
            Assert.Single(model.Entries);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact(DisplayName = "handles_utf8_with_bom")]
    public async Task Handles_utf8_with_bom()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("utf8_bom.m3u8");

        // Assert — accent must round-trip
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Édith Piaf", only.Artist);
    }

    [Fact(DisplayName = "handles_utf8_without_bom")]
    public async Task Handles_utf8_without_bom()
    {
        // Arrange + Act
        PlaylistFileModel model = await ReadAsync("utf8_no_bom.m3u8");

        // Assert
        PlaylistFileEntry only = Assert.Single(model.Entries);
        Assert.Equal("Édith Piaf", only.Artist);
    }

    [Fact(DisplayName = "crlf_and_lf_line_endings_supported")]
    public async Task Crlf_and_lf_line_endings_supported()
    {
        // Arrange + Act
        PlaylistFileModel crlf = await ReadAsync("crlf_endings.m3u8");
        PlaylistFileModel lf = await ReadAsync("lf_endings.m3u8");

        // Assert
        Assert.Equal(crlf.Entries.Count, lf.Entries.Count);
    }
}