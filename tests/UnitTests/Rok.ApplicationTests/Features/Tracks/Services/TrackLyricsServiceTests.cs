using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.Lyrics;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Tracks.Requests;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;

namespace Rok.ApplicationTests.Features.Tracks.Services;

public class TrackLyricsServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILyricsService> _lyricsService = new();
    private readonly Mock<IMusicDataApiService> _musicData = new();

    private TrackLyricsService BuildService() =>
        new(_mediator.Object, _lyricsService.Object, _musicData.Object, NullLogger<TrackLyricsService>.Instance);

    [Fact(DisplayName = "CheckLyricsExists should return true when the lyrics service finds a non-None type")]
    public void CheckLyricsExists_ShouldReturnTrue_WhenLyricsExist()
    {
        // Arrange
        _lyricsService.Setup(l => l.CheckLyricsFileExists("song.mp3")).Returns(ELyricsType.Plain);
        TrackLyricsService sut = BuildService();

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
        TrackLyricsService sut = BuildService();

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
        TrackLyricsService sut = BuildService();

        // Act
        LyricsModel? result = await sut.LoadLyricsAsync("song.mp3");

        // Assert
        Assert.Same(expected, result);
    }

    [Theory(DisplayName = "GetAndSaveLyricsFromApiAsync should return false when required track fields are missing")]
    [InlineData("", "Artist", "Album", "Title", 100L)]
    [InlineData("song.mp3", "", "Album", "Title", 100L)]
    [InlineData("song.mp3", "Artist", "", "Title", 100L)]
    [InlineData("song.mp3", "Artist", "Album", "", 100L)]
    [InlineData("song.mp3", "Artist", "Album", "Title", 0L)]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldReturnFalse_WhenRequiredFieldsMissing(
        string musicFile, string artistName, string albumName, string title, long duration)
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = musicFile, ArtistName = artistName, AlbumName = albumName, Title = title, Duration = duration };
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should return false when API retry is not allowed")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldReturnFalse_WhenRetryNotAllowed()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(false);
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.False(result);
        _musicData.Verify(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should update the last-attempt timestamp before calling the API")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldUpdateLastAttempt_BeforeApiCall()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync((MusicDataLyricsDto?)null);
        TrackLyricsService sut = BuildService();

        // Act
        await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        _mediator.Verify(m => m.Send(It.Is<UpdateTrackGetLyricsLastAttemptRequest>(c => c.TrackId == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(track.GetLyricsLastAttempt);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should return false when the API returns null")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldReturnFalse_WhenApiReturnsNull()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync((MusicDataLyricsDto?)null);
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.False(result);
        _lyricsService.Verify(l => l.SaveLyricsAsync(It.IsAny<LyricsModel>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should return false when the API returns lyrics with both fields null")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldReturnFalse_WhenLyricsBothFieldsNull()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                  .ReturnsAsync(new MusicDataLyricsDto { PlainLyrics = null, SyncLyrics = null });
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.False(result);
        _lyricsService.Verify(l => l.SaveLyricsAsync(It.IsAny<LyricsModel>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should save synchronized lyrics when SyncLyrics is provided")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldSaveSynchronized_WhenSyncLyricsProvided()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                  .ReturnsAsync(new MusicDataLyricsDto { SyncLyrics = "[00:01]hello" });
        _lyricsService.Setup(l => l.GetSynchronizedLyricsFileName("song.mp3")).Returns("song.lrc");
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.True(result);
        _lyricsService.Verify(l => l.SaveLyricsAsync(It.Is<LyricsModel>(m => m.File == "song.lrc" && m.LyricsType == ELyricsType.Synchronized)), Times.Once);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should save plain lyrics when only PlainLyrics is provided")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldSavePlain_WhenOnlyPlainLyricsProvided()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                  .ReturnsAsync(new MusicDataLyricsDto { PlainLyrics = "hello world" });
        _lyricsService.Setup(l => l.GetPlainLyricsFileName("song.mp3")).Returns("song.txt");
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.True(result);
        _lyricsService.Verify(l => l.SaveLyricsAsync(It.Is<LyricsModel>(m => m.File == "song.txt" && m.LyricsType == ELyricsType.Plain)), Times.Once);
    }

    [Fact(DisplayName = "GetAndSaveLyricsFromApiAsync should return false when the API throws an exception")]
    public async Task GetAndSaveLyricsFromApiAsync_ShouldReturnFalse_WhenApiThrows()
    {
        // Arrange
        TrackDto track = new() { Id = 1, MusicFile = "song.mp3", ArtistName = "A", AlbumName = "B", Title = "C", Duration = 100 };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetLyricsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                  .ThrowsAsync(new InvalidOperationException("api down"));
        TrackLyricsService sut = BuildService();

        // Act
        bool result = await sut.GetAndSaveLyricsFromApiAsync(track);

        // Assert
        Assert.False(result);
    }
}
