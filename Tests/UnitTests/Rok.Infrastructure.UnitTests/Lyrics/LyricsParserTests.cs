using Rok.Application.Dto.Lyrics;
using Rok.Infrastructure.Lyrics;

namespace Rok.Infrastructure.UnitTests.Lyrics;

public class LyricsParserTests
{
    [Fact]
    public void Parse_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        LyricsParser sut = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.Parse(""));
        Assert.Throws<ArgumentNullException>(() => sut.Parse(null!));
    }

    [Fact]
    public void Parse_WithSimpleLrc_ReturnsCorrectModel()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00]Line 1\r\n[00:17.20]Line 2\r\n[00:21.10]Line 3";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(3, result.Lyrics.Count);
        Assert.Equal(3, result.Time.Count);
        Assert.Equal("Line 1", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Lyrics[0].Time);
        Assert.Equal("Line 2", result.Lyrics[1].Lyric);
        Assert.Equal(new TimeSpan(0, 0, 17), result.Lyrics[1].Time);
        Assert.Equal("Line 3", result.Lyrics[2].Lyric);
        Assert.Equal(new TimeSpan(0, 0, 21), result.Lyrics[2].Time);
    }

    [Fact]
    public void Parse_WithoutCentiseconds_ParsesCorrectly()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12]First line\r\n[01:30]Second line";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(2, result.Lyrics.Count);
        Assert.Equal("First line", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Lyrics[0].Time);
        Assert.Equal("Second line", result.Lyrics[1].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(90), result.Lyrics[1].Time);
    }

    [Fact]
    public void Parse_WithMultipleTimestampsPerLine_CreatesMultipleEntries()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00][00:24.00]Chorus line";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(2, result.Lyrics.Count);
        Assert.Equal("Chorus line", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Lyrics[0].Time);
        Assert.Equal("Chorus line", result.Lyrics[1].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(24), result.Lyrics[1].Time);
    }

    [Fact]
    public void Parse_WithMetadataTags_IgnoresThem()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[ar:Artist Name]\r\n[ti:Song Title]\r\n[00:12.00]First lyric line";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Single(result.Lyrics);
        Assert.Equal("First lyric line", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(12), result.Lyrics[0].Time);
    }

    [Fact]
    public void Parse_WithEmptyLyricLines_SkipsThem()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00]\r\n[00:15.00]Line with text\r\n[00:18.00]";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Single(result.Lyrics);
        Assert.Equal("Line with text", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(15), result.Lyrics[0].Time);
    }

    [Fact]
    public void Parse_WithDifferentLineEndings_HandlesAll()
    {
        // Arrange
        LyricsParser sut = new();
        string lyricsRN = "[00:12.00]Line 1\r\n[00:15.00]Line 2";
        string lyricsN = "[00:12.00]Line 1\n[00:15.00]Line 2";
        string lyricsR = "[00:12.00]Line 1\r[00:15.00]Line 2";

        // Act
        SyncLyricsModel resultRN = sut.Parse(lyricsRN);
        SyncLyricsModel resultN = sut.Parse(lyricsN);
        SyncLyricsModel resultR = sut.Parse(lyricsR);

        // Assert
        Assert.Equal(2, resultRN.Lyrics.Count);
        Assert.Equal(2, resultN.Lyrics.Count);
        Assert.Equal(2, resultR.Lyrics.Count);
    }

    [Fact]
    public void Parse_WithInvalidTimestamps_IgnoresThem()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[invalid]Line 1\r\n[00:15.00]Line 2\r\n[99:99:99]Line 3";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(2, result.Lyrics.Count);
        Assert.Equal("Line 1", result.Lyrics[0].Lyric);
        Assert.Equal("Line 2", result.Lyrics[1].Lyric);
        Assert.Equal("00:00:00", result.Lyrics[0].Time.ToString());
        Assert.Equal("00:00:15", result.Lyrics[1].Time.ToString());
    }

    [Fact]
    public void Parse_SortsLyricsByTime()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:30.00]Third line\r\n[00:10.00]First line\r\n[00:20.00]Second line";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(3, result.Lyrics.Count);
        Assert.Equal("First line", result.Lyrics[0].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(10), result.Lyrics[0].Time);
        Assert.Equal("Second line", result.Lyrics[1].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(20), result.Lyrics[1].Time);
        Assert.Equal("Third line", result.Lyrics[2].Lyric);
        Assert.Equal(TimeSpan.FromSeconds(30), result.Lyrics[2].Time);
    }

    [Fact]
    public void Parse_WithComplexLrcFile_ParsesCorrectly()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = @"[ar:Example Artist]
[ti:Example Song]
[al:Example Album]
[by:Creator]
[00:12.00]First line of lyrics
[00:17.20]Second line of lyrics
[00:21.10]Third line of lyrics
[00:24.00]Fourth line of lyrics
[00:28.50]Fifth line of lyrics";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(5, result.Lyrics.Count);
        Assert.Equal(5, result.Time.Count);
        Assert.Equal("First line of lyrics", result.Lyrics[0].Lyric);
        Assert.Equal("Fifth line of lyrics", result.Lyrics[4].Lyric);
    }

    [Fact]
    public void Parse_WithLongMinutes_ParsesCorrectly()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[05:30.00]Line at 5:30\r\n[12:45.00]Line at 12:45";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(2, result.Lyrics.Count);
        Assert.Equal(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)), result.Lyrics[0].Time);
        Assert.Equal(TimeSpan.FromMinutes(12).Add(TimeSpan.FromSeconds(45)), result.Lyrics[1].Time);
    }

    [Fact]
    public void Parse_WithSpecialCharactersInLyrics_PreservesThem()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00]Spécial charactérs: é à ù ç\r\n[00:15.00]Symbols: @#$%&*()";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(2, result.Lyrics.Count);
        Assert.Equal("Spécial charactérs: é à ù ç", result.Lyrics[0].Lyric);
        Assert.Equal("Symbols: @#$%&*()", result.Lyrics[1].Lyric);
    }

    [Fact]
    public void Parse_WithOnlyMetadata_ReturnsEmptyModel()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[ar:Artist]\r\n[ti:Title]\r\n[al:Album]";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Empty(result.Lyrics);
        Assert.Empty(result.Time);
    }

    [Fact]
    public void Parse_WithWhitespaceInLyrics_PreservesIt()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00]  Line with spaces  ";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Single(result.Lyrics);
        Assert.Equal("  Line with spaces  ", result.Lyrics[0].Lyric);
    }

    [Fact]
    public void Parse_TimeListMatchesLyricsList()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = "[00:12.00]Line 1\r\n[00:15.00]Line 2\r\n[00:18.00]Line 3";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(result.Lyrics.Count, result.Time.Count);
        for (int i = 0; i < result.Lyrics.Count; i++)
        {
            Assert.Equal(result.Time[i], result.Lyrics[i].Time);
        }
    }

    [Fact]
    public void Parse_WithMixedValidAndInvalidLines_ParsesValidOnes()
    {
        // Arrange
        LyricsParser sut = new();
        string lyrics = @"[00:12.00]Valid line 1
Invalid line without timestamp
[00:15.00]Valid line 2
Another invalid line
[00:18.00]Valid line 3";

        // Act
        SyncLyricsModel result = sut.Parse(lyrics);

        // Assert
        Assert.Equal(3, result.Lyrics.Count);
        Assert.Equal("Valid line 1", result.Lyrics[0].Lyric);
        Assert.Equal("Valid line 2", result.Lyrics[1].Lyric);
        Assert.Equal("Valid line 3", result.Lyrics[2].Lyric);
    }
}