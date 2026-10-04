using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Pictures;
using Rok.ViewModels.Playlist.Services;

namespace Rok.PresentationTests.ViewModels.Playlist.Services;

public class PlaylistPictureServiceTests
{
    private readonly Mock<IArtistPicture> _artistPicture = new();

    private PlaylistPictureService BuildService() => new(_artistPicture.Object, NullLogger<PlaylistPictureService>.Instance);

    [Theory(DisplayName = "load_picture_should_return_null_without_a_picture_name")]
    [InlineData(null)]
    [InlineData("")]
    public void LoadPicture_ShouldReturnNull_WithoutPictureName(string? pictureName)
    {
        // Arrange
        PlaylistPictureService sut = BuildService();

        // Act
        var picture = sut.LoadPicture(pictureName);

        // Assert
        Assert.Null(picture);
    }

    [Fact(DisplayName = "load_picture_should_return_null_when_the_file_is_missing")]
    public void LoadPicture_ShouldReturnNull_WhenFileIsMissing()
    {
        // Arrange
        _artistPicture.Setup(p => p.PictureFileExists("playlist")).Returns(false);
        PlaylistPictureService sut = BuildService();

        // Act
        var picture = sut.LoadPicture("playlist");

        // Assert
        Assert.Null(picture);
    }
}