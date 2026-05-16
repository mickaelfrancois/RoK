using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto.MusicDataApi;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;

namespace Rok.ApplicationTests.Features.Artists.Services;

public class ArtistApiServiceTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IMusicDataApiService> _musicData = new();
    private readonly Mock<IArtistPictureService> _pictureService = new();
    private readonly Mock<IBackdropPicture> _backdropPicture = new();

    private ArtistApiService BuildService() => new(_mediator.Object, _musicData.Object, NullLogger<ArtistApiService>.Instance);

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return None when artist name is empty")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnNone_WhenNameMissing()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "" };
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.Equal(ArtistApiUpdateResult.None, result);
        _musicData.Verify(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return None when API retry is not allowed")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnNone_WhenRetryNotAllowed()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(false);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.Equal(ArtistApiUpdateResult.None, result);
        _musicData.Verify(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should update last-attempt timestamp before calling the API")]
    public async Task GetAndUpdateArtistDataAsync_ShouldUpdateLastAttempt_BeforeApiCall()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync((MusicDataArtistDto?)null);
        ArtistApiService sut = BuildService();

        // Act
        await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        _mediator.Verify(m => m.Send(It.Is<UpdateArtistGetMetaDataLastAttemptRequest>(c => c.ArtistId == 1), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(artist.GetMetaDataLastAttempt);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return None when the API returns null")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnNone_WhenApiReturnsNull()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync((MusicDataArtistDto?)null);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.Equal(ArtistApiUpdateResult.None, result);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should return None when the API response has no MusicBrainz id")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReturnNone_WhenApiResponseHasNoMbid()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>()))
                  .ReturnsAsync(new MusicDataArtistDto { MusicBrainzID = string.Empty });
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.Equal(ArtistApiUpdateResult.None, result);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should report PictureDownloaded when the picture is fetched and now exists")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReportPictureDownloaded_WhenPictureFetched()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid", PictureUrl = "https://example.com/pic.jpg" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.SetupSequence(p => p.PictureExists(artist.Name)).Returns(false).Returns(true);
        _pictureService.Setup(p => p.GetPictureFilePath(artist.Name)).Returns("C:/tmp/Beatles.jpg");
        _backdropPicture.Setup(b => b.HasBackdrops(artist.Name)).Returns(true);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.True(result.PictureDownloaded);
        Assert.False(result.BackdropsDownloaded);
        _musicData.Verify(m => m.DownloadArtistPictureAsync(artistApi, "C:/tmp/Beatles.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should not report PictureDownloaded when the picture already exists locally")]
    public async Task GetAndUpdateArtistDataAsync_ShouldNotReportPictureDownloaded_WhenPictureAlreadyExists()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid", PictureUrl = "https://example.com/pic.jpg" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.Setup(p => p.PictureExists(artist.Name)).Returns(true);
        _backdropPicture.Setup(b => b.HasBackdrops(artist.Name)).Returns(true);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result.PictureDownloaded);
        _musicData.Verify(m => m.DownloadArtistPictureAsync(It.IsAny<MusicDataArtistDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should not report PictureDownloaded when the API picture URL is empty")]
    public async Task GetAndUpdateArtistDataAsync_ShouldNotReportPictureDownloaded_WhenApiPictureUrlIsEmpty()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid", PictureUrl = string.Empty };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.Setup(p => p.PictureExists(artist.Name)).Returns(false);
        _backdropPicture.Setup(b => b.HasBackdrops(artist.Name)).Returns(true);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result.PictureDownloaded);
        _musicData.Verify(m => m.DownloadArtistPictureAsync(It.IsAny<MusicDataArtistDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should report BackdropsDownloaded when backdrops are fetched and the folder is now populated")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReportBackdropsDownloaded_WhenBackdropsFetched()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.Setup(p => p.PictureExists(artist.Name)).Returns(true);
        _backdropPicture.SetupSequence(b => b.HasBackdrops(artist.Name)).Returns(false).Returns(true);
        _backdropPicture.Setup(b => b.GetArtistPictureFolder(artist.Name)).Returns("C:/tmp/backdrops");
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.True(result.BackdropsDownloaded);
        _musicData.Verify(m => m.DownloadArtistBackdropsAsync(artistApi, "C:/tmp/backdrops", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should not report BackdropsDownloaded when backdrops already exist")]
    public async Task GetAndUpdateArtistDataAsync_ShouldNotReportBackdropsDownloaded_WhenBackdropsAlreadyExist()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.Setup(p => p.PictureExists(artist.Name)).Returns(true);
        _backdropPicture.Setup(b => b.HasBackdrops(artist.Name)).Returns(true);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.False(result.BackdropsDownloaded);
        _musicData.Verify(m => m.DownloadArtistBackdropsAsync(It.IsAny<MusicDataArtistDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "GetAndUpdateArtistDataAsync should report PictureDownloaded independently of DataUpdated")]
    public async Task GetAndUpdateArtistDataAsync_ShouldReportPictureDownloaded_EvenWhenNoDataChanged()
    {
        // Arrange
        ArtistDto artist = new() { Id = 1, Name = "Beatles", MusicBrainzID = "mbid" };
        MusicDataArtistDto artistApi = new() { MusicBrainzID = "mbid", PictureUrl = "https://example.com/pic.jpg" };
        _musicData.Setup(m => m.IsApiRetryAllowed(It.IsAny<DateTime?>())).Returns(true);
        _musicData.Setup(m => m.GetArtistAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(artistApi);
        _pictureService.SetupSequence(p => p.PictureExists(artist.Name)).Returns(false).Returns(true);
        _pictureService.Setup(p => p.GetPictureFilePath(artist.Name)).Returns("C:/tmp/Beatles.jpg");
        _backdropPicture.Setup(b => b.HasBackdrops(artist.Name)).Returns(true);
        ArtistApiService sut = BuildService();

        // Act
        ArtistApiUpdateResult result = await sut.GetAndUpdateArtistDataAsync(artist, _pictureService.Object, _backdropPicture.Object);

        // Assert
        Assert.True(result.PictureDownloaded);
        Assert.False(result.DataUpdated);
        _mediator.Verify(m => m.Send(It.IsAny<UpdateArtistRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
