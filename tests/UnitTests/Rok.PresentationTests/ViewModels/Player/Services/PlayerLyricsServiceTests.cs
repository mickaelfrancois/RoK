using Moq;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Interfaces;
using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class PlayerLyricsServiceTests
{
    private readonly Mock<ILyricsService> _lyricsService = new();
    private readonly Mock<ILyricsParser> _lyricsParser = new();

    private PlayerLyricsService BuildService() => new(_lyricsService.Object, _lyricsParser.Object);

    [Fact(DisplayName = "CheckLyricsExists should return true when the lyrics service finds a non-None type")]
    public void CheckLyricsExists_ShouldReturnTrue_WhenLyricsExist()
    {
        // Arrange
        _lyricsService.Setup(l => l.CheckLyricsFileExists("song.mp3")).Returns(ELyricsType.Synchronized);
        PlayerLyricsService sut = BuildService();

        // Act
        bool exists = sut.CheckLyricsExists("song.mp3");

        // Assert
        Assert.True(exists);
    }

    [Fact(DisplayName = "CheckLyricsExists should return false when the lyrics service returns None")]
    public void CheckLyricsExists_ShouldReturnFalse_WhenNoLyrics()
    {
        // Arrange
        _lyricsService.Setup(l => l.CheckLyricsFileExists(It.IsAny<string>())).Returns(ELyricsType.None);
        PlayerLyricsService sut = BuildService();

        // Act
        bool exists = sut.CheckLyricsExists("song.mp3");

        // Assert
        Assert.False(exists);
    }

    [Fact(DisplayName = "LoadLyricsAsync should delegate to the lyrics service")]
    public async Task LoadLyricsAsync_ShouldDelegateToLyricsService()
    {
        // Arrange
        LyricsModel expected = new() { File = "song.lrc", LyricsType = ELyricsType.Plain };
        _lyricsService.Setup(l => l.LoadLyricsAsync("song.mp3")).ReturnsAsync(expected);
        PlayerLyricsService sut = BuildService();

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync("song.mp3");

        // Assert
        Assert.Same(expected, result);
    }

    [Fact(DisplayName = "ParseSynchronizedLyrics should delegate to the lyrics parser and return its result")]
    public void ParseSynchronizedLyrics_ShouldDelegateToLyricsParser()
    {
        // Arrange
        string lrc = "[00:00.00]first line\n[00:05.00]second line";
        SyncLyricsModel expected = new();
        _lyricsParser.Setup(p => p.Parse(lrc)).Returns(expected);
        PlayerLyricsService sut = BuildService();

        // Act
        SyncLyricsModel result = sut.ParseSynchronizedLyrics(lrc);

        // Assert
        Assert.Same(expected, result);
    }
}
