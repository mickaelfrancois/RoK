using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;

namespace Rok.ApplicationTests.Features.Albums.Services;

public class AlbumApiServiceTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IMusicDataApiService> _musicData = new();
    private readonly Mock<IAlbumPictureService> _pictureService = new();

    private AlbumApiService BuildService() => new(_mediator, _musicData.Object, NullLogger<AlbumApiService>.Instance);

    [Theory(DisplayName = "GetAndUpdateAlbumDataAsync should return None when album name or artist name is empty")]
    [InlineData("", "Beatles")]
    [InlineData("Album", "")]
    [InlineData("", "")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnNone_WhenNamesMissing(string name, string artistName)
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = name, ArtistName = artistName };
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.Equal(AlbumApiUpdateResult.None, result);
        _musicData.Verify(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return None when API retry is not allowed")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnNone_WhenRetryNotAllowed()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(false);
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.Equal(AlbumApiUpdateResult.None, result);
        _musicData.Verify(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        Assert.Empty(_mediator.Sent<UpdateAlbumGetMetaDataLastAttemptRequest>());
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should update last-attempt timestamp before calling the API")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldUpdateLastAttempt_BeforeApiCall()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((MusicDataAlbumDto?)null);
        AlbumApiService sut = BuildService();

        // Act
        await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        UpdateAlbumGetMetaDataLastAttemptRequest sent = Assert.Single(_mediator.Sent<UpdateAlbumGetMetaDataLastAttemptRequest>());
        Assert.Equal(1, sent.AlbumId);
        Assert.NotNull(album.GetMetaDataLastAttempt);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return None when the API returns null")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnNone_WhenApiReturnsNull()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync((MusicDataAlbumDto?)null);
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.Equal(AlbumApiUpdateResult.None, result);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should return None when the API response has no MusicBrainz id")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReturnNone_WhenApiResponseHasNoMbid()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                  .ReturnsAsync(new MusicDataAlbumDto { MusicBrainzID = string.Empty });
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.Equal(AlbumApiUpdateResult.None, result);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should report PictureDownloaded when the cover is fetched and now exists")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReportPictureDownloaded_WhenCoverFetched()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist", AlbumPath = "C:/music/Album" };
        MusicDataAlbumDto albumApi = new() { MusicBrainzID = "mbid" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(albumApi);
        _pictureService.SetupSequence(p => p.PictureExists(album.AlbumPath)).Returns(false).Returns(true);
        _pictureService.Setup(p => p.GetPictureFilePath(album.AlbumPath)).Returns("C:/music/Album/cover.jpg");
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.True(result.PictureDownloaded);
        _musicData.Verify(m => m.DownloadCoverAsync(albumApi, "C:/music/Album/cover.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should not report PictureDownloaded when the cover already exists locally")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldNotReportPictureDownloaded_WhenCoverAlreadyExists()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist", AlbumPath = "C:/music/Album" };
        MusicDataAlbumDto albumApi = new() { MusicBrainzID = "mbid" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(albumApi);
        _pictureService.Setup(p => p.PictureExists(album.AlbumPath)).Returns(true);
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.False(result.PictureDownloaded);
        _musicData.Verify(m => m.DownloadCoverAsync(It.IsAny<MusicDataAlbumDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateAlbumDataAsync should report PictureDownloaded independently of DataUpdated")]
    public async Task GetAndUpdateAlbumDataAsync_ShouldReportPictureDownloaded_EvenWhenNoDataChanged()
    {
        // Arrange
        AlbumDto album = new() { Id = 1, Name = "Album", ArtistName = "Artist", AlbumPath = "C:/music/Album", MusicBrainzID = "mbid" };
        MusicDataAlbumDto albumApi = new() { MusicBrainzID = "mbid" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetAlbumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(albumApi);
        _pictureService.SetupSequence(p => p.PictureExists(album.AlbumPath)).Returns(false).Returns(true);
        _pictureService.Setup(p => p.GetPictureFilePath(album.AlbumPath)).Returns("C:/music/Album/cover.jpg");
        AlbumApiService sut = BuildService();

        // Act
        AlbumApiUpdateResult result = await sut.GetAndUpdateAlbumDataAsync(album, _pictureService.Object);

        // Assert
        Assert.True(result.PictureDownloaded);
        Assert.False(result.DataUpdated);
        Assert.Empty(_mediator.Sent<UpdateAlbumRequest>());
    }
}