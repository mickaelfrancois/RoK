using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class PostImportDominantColorTaskTests
{
    private static PostImportDominantColorTask BuildTask(
        Mock<IDominantColorCalculator> calculator,
        Mock<IAlbumRepository> albumRepository,
        Mock<IAlbumPictureService> albumPicture,
        Mock<IArtistRepository> artistRepository,
        Mock<IArtistPictureService> artistPicture)
    {
        return new PostImportDominantColorTask(
            calculator.Object,
            albumRepository.Object,
            albumPicture.Object,
            artistRepository.Object,
            artistPicture.Object,
            NullLogger<PostImportDominantColorTask>.Instance);
    }

    [Fact(DisplayName = "ProcessAlbums should skip albums that already have a dominant color")]
    public async Task ProcessAlbums_ShouldSkipAlbums_ThatAlreadyHaveDominantColor()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, AlbumPath = @"C:\A", PictureDominantColor = 123 }
        };
        Mock<IDominantColorCalculator> calculator = new();
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IAlbumPictureService> albumPicture = new();
        Mock<IArtistRepository> artistRepo = new();
        artistRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<ArtistEntity>());
        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, albumPicture, artistRepo, new());

        // Act
        await task.ProcessAlbumsAsync(CancellationToken.None);

        // Assert
        calculator.Verify(c => c.CalculateAsync(It.IsAny<string>()), Times.Never);
        albumRepo.Verify(r => r.UpdatePictureDominantColorAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "ProcessAlbums should skip albums whose picture file does not exist on disk")]
    public async Task ProcessAlbums_ShouldSkipAlbums_WhosePictureFileDoesNotExist()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, AlbumPath = @"C:\A" }
        };
        Mock<IDominantColorCalculator> calculator = new();
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IAlbumPictureService> albumPicture = new();
        albumPicture.Setup(p => p.PictureExists(@"C:\A")).Returns(false);

        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, albumPicture, new(), new());

        // Act
        await task.ProcessAlbumsAsync(CancellationToken.None);

        // Assert
        calculator.Verify(c => c.CalculateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "ProcessAlbums should update dominant color when calculator returns a value")]
    public async Task ProcessAlbums_ShouldUpdateDominantColor_WhenCalculatorReturnsValue()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, AlbumPath = @"C:\A" }
        };
        Mock<IDominantColorCalculator> calculator = new();
        calculator.Setup(c => c.CalculateAsync(@"C:\A\cover.jpg")).ReturnsAsync(255L);
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IAlbumPictureService> albumPicture = new();
        albumPicture.Setup(p => p.PictureExists(@"C:\A")).Returns(true);
        albumPicture.Setup(p => p.GetPictureFilePath(@"C:\A")).Returns(@"C:\A\cover.jpg");

        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, albumPicture, new(), new());

        // Act
        await task.ProcessAlbumsAsync(CancellationToken.None);

        // Assert
        albumRepo.Verify(r => r.UpdatePictureDominantColorAsync(1, 255L, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "ProcessAlbums should not update dominant color when calculator returns null")]
    public async Task ProcessAlbums_ShouldNotUpdateDominantColor_WhenCalculatorReturnsNull()
    {
        // Arrange
        List<AlbumEntity> albums = new() { new() { Id = 1, AlbumPath = @"C:\A" } };
        Mock<IDominantColorCalculator> calculator = new();
        calculator.Setup(c => c.CalculateAsync(It.IsAny<string>())).ReturnsAsync((long?)null);
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IAlbumPictureService> albumPicture = new();
        albumPicture.Setup(p => p.PictureExists(It.IsAny<string>())).Returns(true);
        albumPicture.Setup(p => p.GetPictureFilePath(It.IsAny<string>())).Returns(@"C:\A\cover.jpg");

        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, albumPicture, new(), new());

        // Act
        await task.ProcessAlbumsAsync(CancellationToken.None);

        // Assert
        albumRepo.Verify(r => r.UpdatePictureDominantColorAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "ProcessAlbums should stop iterating once the cancellation token is triggered")]
    public async Task ProcessAlbums_ShouldStopIterating_OnceCancellationTokenIsTriggered()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, AlbumPath = @"C:\A" },
            new() { Id = 2, AlbumPath = @"C:\B" }
        };
        Mock<IDominantColorCalculator> calculator = new();
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IAlbumPictureService> albumPicture = new();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, albumPicture, new(), new());

        // Act
        await task.ProcessAlbumsAsync(cts.Token);

        // Assert
        albumPicture.Verify(p => p.PictureExists(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "ProcessArtists should update dominant color from artist picture path")]
    public async Task ProcessArtists_ShouldUpdateDominantColor_FromArtistPicturePath()
    {
        // Arrange
        List<ArtistEntity> artists = new() { new() { Id = 9, Name = "Queen" } };
        Mock<IDominantColorCalculator> calculator = new();
        calculator.Setup(c => c.CalculateAsync(@"C:\pics\Queen.jpg")).ReturnsAsync(42L);
        Mock<IArtistRepository> artistRepo = new();
        artistRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        Mock<IArtistPictureService> artistPicture = new();
        artistPicture.Setup(p => p.PictureExists("Queen")).Returns(true);
        artistPicture.Setup(p => p.GetPictureFilePath("Queen")).Returns(@"C:\pics\Queen.jpg");

        PostImportDominantColorTask task = BuildTask(calculator, new(), new(), artistRepo, artistPicture);

        // Act
        await task.ProcessArtistsAsync(CancellationToken.None);

        // Assert
        artistRepo.Verify(r => r.UpdatePictureDominantColorAsync(9, 42L, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "RunAsync should process albums then artists in sequence")]
    public async Task RunAsync_ShouldProcessAlbums_ThenArtists_InSequence()
    {
        // Arrange
        Mock<IDominantColorCalculator> calculator = new();
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<AlbumEntity>());
        Mock<IArtistRepository> artistRepo = new();
        artistRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<ArtistEntity>());

        PostImportDominantColorTask task = BuildTask(calculator, albumRepo, new(), artistRepo, new());

        // Act
        await task.RunAsync(CancellationToken.None);

        // Assert
        albumRepo.Verify(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()), Times.Once);
        artistRepo.Verify(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}
