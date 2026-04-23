using Moq;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Lyrics;

namespace Rok.Infrastructure.UnitTests.Lyrics;

public class LyricsServiceTests
{
    private const string MusicFile = @"C:\Music\Artist\Album\Track.mp3";
    private const string ExpectedLrcFile = @"C:\Music\Artist\Album\Track.lrc";
    private const string ExpectedTxtFile = @"C:\Music\Artist\Album\Track.txt";

    private static Mock<IFileSystem> CreateFileSystemMock()
    {
        Mock<IFileSystem> mock = new(MockBehavior.Strict);
        mock.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns<string>(path => Path.GetDirectoryName(path));
        mock.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns<string>(path => Path.GetFileNameWithoutExtension(path));
        mock.Setup(fs => fs.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>((path1, path2) => Path.Combine(path1, path2));
        return mock;
    }

    [Fact]
    public void GetSynchronizedLyricsFileName_WithValidPath_ReturnsLrcFile()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act
        string result = sut.GetSynchronizedLyricsFileName(MusicFile);

        // Assert
        Assert.Equal(ExpectedLrcFile, result);
    }

    [Fact]
    public void GetSynchronizedLyricsFileName_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetSynchronizedLyricsFileName(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetSynchronizedLyricsFileName(null!));
    }

    [Fact]
    public void GetSynchronizedLyricsFileName_WithDifferentExtensions_ReplacesWithLrc()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);
        string flacFile = @"C:\Music\Track.flac";
        string wavFile = @"C:\Music\Track.wav";

        // Act
        string resultFlac = sut.GetSynchronizedLyricsFileName(flacFile);
        string resultWav = sut.GetSynchronizedLyricsFileName(wavFile);

        // Assert
        Assert.Equal(@"C:\Music\Track.lrc", resultFlac);
        Assert.Equal(@"C:\Music\Track.lrc", resultWav);
    }

    [Fact]
    public void GetPlainLyricsFileName_WithValidPath_ReturnsTxtFile()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act
        string result = sut.GetPlainLyricsFileName(MusicFile);

        // Assert
        Assert.Equal(ExpectedTxtFile, result);
    }

    [Fact]
    public void GetPlainLyricsFileName_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetPlainLyricsFileName(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetPlainLyricsFileName(null!));
    }

    [Fact]
    public void GetPlainLyricsFileName_WithDifferentExtensions_ReplacesWithTxt()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);
        string flacFile = @"C:\Music\Track.flac";

        // Act
        string result = sut.GetPlainLyricsFileName(flacFile);

        // Assert
        Assert.Equal(@"C:\Music\Track.txt", result);
    }

    [Fact]
    public void CheckLyricsFileExists_WithNoFiles_ReturnsNone()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(false);
        fs.Setup(f => f.FileExists(ExpectedTxtFile)).Returns(false);
        LyricsService sut = new(fs.Object);

        // Act
        ELyricsType result = sut.CheckLyricsFileExists(MusicFile);

        // Assert
        Assert.Equal(ELyricsType.None, result);
        fs.Verify(f => f.FileExists(ExpectedLrcFile), Times.Once);
        fs.Verify(f => f.FileExists(ExpectedTxtFile), Times.Once);
    }

    [Fact]
    public void CheckLyricsFileExists_WithLrcFile_ReturnsSynchronized()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(true);
        LyricsService sut = new(fs.Object);

        // Act
        ELyricsType result = sut.CheckLyricsFileExists(MusicFile);

        // Assert
        Assert.Equal(ELyricsType.Synchronized, result);
        fs.Verify(f => f.FileExists(ExpectedLrcFile), Times.Once);
        fs.Verify(f => f.FileExists(ExpectedTxtFile), Times.Never);
    }

    [Fact]
    public void CheckLyricsFileExists_WithTxtFile_ReturnsPlain()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(false);
        fs.Setup(f => f.FileExists(ExpectedTxtFile)).Returns(true);
        LyricsService sut = new(fs.Object);

        // Act
        ELyricsType result = sut.CheckLyricsFileExists(MusicFile);

        // Assert
        Assert.Equal(ELyricsType.Plain, result);
        fs.Verify(f => f.FileExists(ExpectedLrcFile), Times.Once);
        fs.Verify(f => f.FileExists(ExpectedTxtFile), Times.Once);
    }

    [Fact]
    public void CheckLyricsFileExists_WithBothFiles_PrioritizesLrc()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(true);
        fs.Setup(f => f.FileExists(ExpectedTxtFile)).Returns(true);
        LyricsService sut = new(fs.Object);

        // Act
        ELyricsType result = sut.CheckLyricsFileExists(MusicFile);

        // Assert
        Assert.Equal(ELyricsType.Synchronized, result);
        fs.Verify(f => f.FileExists(ExpectedLrcFile), Times.Once);
        fs.Verify(f => f.FileExists(ExpectedTxtFile), Times.Never);
    }

    [Fact]
    public void CheckLyricsFileExists_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.CheckLyricsFileExists(""));
        Assert.Throws<ArgumentNullException>(() => sut.CheckLyricsFileExists(null!));
    }

    [Fact]
    public async Task LoadLyricsAsync_WithNoFiles_ReturnsNull()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(false);
        fs.Setup(f => f.FileExists(ExpectedTxtFile)).Returns(false);
        LyricsService sut = new(fs.Object);

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync(MusicFile);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadLyricsAsync_WithLrcFile_LoadsSynchronizedLyrics()
    {
        // Arrange
        string lrcContent = "[00:12.00]First line\r\n[00:15.00]Second line";
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(lrcContent);
        LyricsService sut = new(fs.Object);

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync(MusicFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ExpectedLrcFile, result!.File);
        Assert.Equal(ELyricsType.Synchronized, result.LyricsType);
        Assert.Equal(lrcContent, result.SynchronizedLyrics);
        Assert.NotNull(result.PlainLyrics);
        Assert.Contains("First line", result.PlainLyrics);
        Assert.Contains("Second line", result.PlainLyrics);
    }

    [Fact]
    public async Task LoadLyricsAsync_WithTxtFile_LoadsPlainLyrics()
    {
        // Arrange
        string txtContent = "Plain lyrics\r\nLine 2\r\nLine 3";
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(false);
        fs.Setup(f => f.FileExists(ExpectedTxtFile)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(txtContent);
        LyricsService sut = new(fs.Object);

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync(MusicFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ExpectedTxtFile, result!.File);
        Assert.Equal(ELyricsType.Plain, result.LyricsType);
        Assert.Equal(txtContent, result.PlainLyrics);
        Assert.Null(result.SynchronizedLyrics);
    }

    [Fact]
    public async Task LoadLyricsAsync_WithBothFiles_PrioritizesLrc()
    {
        // Arrange
        string lrcContent = "[00:12.00]Synced lyrics";
        Mock<IFileSystem> fs = CreateFileSystemMock();
        fs.Setup(f => f.FileExists(ExpectedLrcFile)).Returns(true);
        fs.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(lrcContent);
        LyricsService sut = new(fs.Object);

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync(MusicFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ELyricsType.Synchronized, result!.LyricsType);
        Assert.Equal(ExpectedLrcFile, result.File);
    }

    [Fact]
    public async Task LoadLyricsAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = CreateFileSystemMock();
        LyricsService sut = new(fs.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.LoadLyricsAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.LoadLyricsAsync(null!));
    }

    [Fact]
    public async Task SaveLyricsAsync_WritesContentToFile()
    {
        // Arrange
        string lyricsFile = @"C:\Music\lyrics.txt";
        string content = "Test lyrics content\r\nLine 2";
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        LyricsService sut = new(fs.Object);
        LyricsModel lyrics = new()
        {
            File = lyricsFile,
            PlainLyrics = content
        };

        // Act
        await sut.SaveLyricsAsync(lyrics);

        // Assert
        fs.Verify(f => f.WriteAllTextAsync(lyricsFile, content, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetRawLyrics_WithEmptyString_ReturnsEmpty()
    {
        // Act
        string result = LyricsService.GetRawLyrics("");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void GetRawLyrics_WithNull_ReturnsNull()
    {
        // Act
        string result = LyricsService.GetRawLyrics(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetRawLyrics_RemovesTimestamps()
    {
        // Arrange
        string syncLyrics = "[00:12.00]First line\r\n[00:15.00]Second line";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
        Assert.Contains("First line", result);
        Assert.Contains("Second line", result);
    }

    [Fact]
    public void GetRawLyrics_RemovesMetadataTags()
    {
        // Arrange
        string syncLyrics = "[ar:Artist]\r\n[ti:Title]\r\n[00:12.00]Lyrics line";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.DoesNotContain("[ar:Artist]", result);
        Assert.DoesNotContain("[ti:Title]", result);
        Assert.Contains("Lyrics line", result);
    }

    [Fact]
    public void GetRawLyrics_TrimsWhitespace()
    {
        // Arrange
        string syncLyrics = "[00:12.00]  First line  \r\n[00:15.00]  Second line  ";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.Equal("First line\r\nSecond line", result);
    }

    [Fact]
    public void GetRawLyrics_RemovesLeadingEmptyLines()
    {
        // Arrange
        string syncLyrics = "\r\n\r\n[00:12.00]First line";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.StartsWith("First line", result);
    }

    [Fact]
    public void GetRawLyrics_RemovesTrailingEmptyLines()
    {
        // Arrange
        string syncLyrics = "[00:12.00]First line\r\n\r\n\r\n";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.EndsWith("First line", result);
    }

    [Fact]
    public void GetRawLyrics_PreservesEmptyLinesInMiddle()
    {
        // Arrange
        string syncLyrics = "[00:12.00]First line\r\n\r\n[00:18.00]Third line";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        string[] lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None);
        Assert.Equal(3, lines.Length);
        Assert.Equal("First line", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal("Third line", lines[2]);
    }

    [Fact]
    public void GetRawLyrics_WithOnlyWhitespace_ReturnsEmpty()
    {
        // Arrange
        string syncLyrics = "   \r\n   \r\n   ";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetRawLyrics_WithMultipleBrackets_RemovesAll()
    {
        // Arrange
        string syncLyrics = "[00:12.00][00:24.00]Chorus line";

        // Act
        string result = LyricsService.GetRawLyrics(syncLyrics);

        // Assert
        Assert.Equal("Chorus line", result);
    }

    [Fact]
    public void GetRawLyrics_WithDifferentLineEndings_HandlesAll()
    {
        // Arrange
        string syncLyricsRN = "[00:12.00]Line 1\r\n[00:15.00]Line 2";
        string syncLyricsN = "[00:12.00]Line 1\n[00:15.00]Line 2";
        string syncLyricsR = "[00:12.00]Line 1\r[00:15.00]Line 2";

        // Act
        string resultRN = LyricsService.GetRawLyrics(syncLyricsRN);
        string resultN = LyricsService.GetRawLyrics(syncLyricsN);
        string resultR = LyricsService.GetRawLyrics(syncLyricsR);

        // Assert
        Assert.Contains("Line 1", resultRN);
        Assert.Contains("Line 2", resultRN);
        Assert.Contains("Line 1", resultN);
        Assert.Contains("Line 2", resultN);
        Assert.Contains("Line 1", resultR);
        Assert.Contains("Line 2", resultR);
    }
}