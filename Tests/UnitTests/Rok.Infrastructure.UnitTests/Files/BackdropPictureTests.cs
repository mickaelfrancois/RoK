using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests.Files;

public class BackdropPictureTests
{
    private const string CachePath = "/cache";
    private const string ArtistName = "Test Artist";
    private const string ArtistFolderName = "Test Artist";
    private static string RepositoryArtistPath => Path.Combine(CachePath, "@Artists");
    private static string ArtistPictureFolder => Path.Combine(RepositoryArtistPath, ArtistFolderName);

    private static BackdropPicture CreateSut()
    {
        Mock<IAppOptions> options = new();
        options.Setup(o => o.CachePath).Returns(CachePath);
        return new BackdropPicture(options.Object, NullLogger<BackdropPicture>.Instance);
    }

    [Fact]
    public void Constructor_SetsRepositoryPath()
    {
        // Arrange
        Mock<IAppOptions> options = new();
        options.Setup(o => o.CachePath).Returns(CachePath);

        // Act
        BackdropPicture sut = new(options.Object, NullLogger<BackdropPicture>.Instance);

        // Assert
        Assert.Equal(RepositoryArtistPath, sut.RepositoryArtistPath);
    }

    [Fact]
    public void SetRepositoryArtistPath_UpdatesPath()
    {
        // Arrange
        BackdropPicture sut = CreateSut();
        string newPath = "/new/cache";
        string expectedRepoPath = Path.Combine(newPath, "@Artists");

        // Act
        sut.SetRepositoryArtistPath(newPath);

        // Assert
        Assert.Equal(expectedRepoPath, sut.RepositoryArtistPath);
    }

    [Fact]
    public void SetRepositoryArtistPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.SetRepositoryArtistPath(""));
        Assert.Throws<ArgumentNullException>(() => sut.SetRepositoryArtistPath(null!));
    }

    [Fact]
    public void GetArtistPictureFolder_ReturnsCorrectPath()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act
        string result = sut.GetArtistPictureFolder(ArtistName);

        // Assert
        Assert.Equal(ArtistPictureFolder, result);
    }

    [Fact]
    public void GetArtistPictureFolder_WithSpecialCharacters_SanitizesName()
    {
        // Arrange
        BackdropPicture sut = CreateSut();
        string artistNameWithSpecialChars = "AC/DC: The Band";

        // Act
        string result = sut.GetArtistPictureFolder(artistNameWithSpecialChars);
        result = result.Substring(CachePath.Length);

        // Assert
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain(":", result);
        Assert.Contains("@Artists", result);
    }

    [Fact]
    public void GetArtistPictureFolder_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetArtistPictureFolder(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetArtistPictureFolder(null!));
    }

    [Fact]
    public void GetBackdrops_WithNonExistentFolder_ReturnsEmptyList()
    {
        // Arrange
        BackdropPicture sut = CreateSut();
        string nonExistentArtist = Guid.NewGuid().ToString();

        // Act
        List<string> result = sut.GetBackdrops(nonExistentArtist);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetBackdrops_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetBackdrops(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetBackdrops(null!));
    }

    [Fact]
    public void HasBackdrops_WithNonExistentFolder_ReturnsFalse()
    {
        // Arrange
        BackdropPicture sut = CreateSut();
        string nonExistentArtist = Guid.NewGuid().ToString();

        // Act
        bool result = sut.HasBackdrops(nonExistentArtist);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasBackdrops_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.HasBackdrops(""));
        Assert.Throws<ArgumentNullException>(() => sut.HasBackdrops(null!));
    }

    [Fact]
    public void GetRandomGenericBackdrop_ReturnsValidPath()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act
        string result = sut.GetRandomGenericBackdrop();

        // Assert
        Assert.StartsWith("ms-appx:///Assets/Backdrop/wallpaper", result);
        Assert.EndsWith(".jpg", result);
    }

    [Fact]
    public void GetRandomGenericBackdrop_ReturnsNumberBetween1And12()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act
        HashSet<string> results = [];
        for (int i = 0; i < 100; i++)
        {
            results.Add(sut.GetRandomGenericBackdrop());
        }

        // Assert
        foreach (string result in results)
        {
            string numberPart = result.Replace("ms-appx:///Assets/Backdrop/wallpaper", "").Replace(".jpg", "");
            int number = int.Parse(numberPart);
            Assert.InRange(number, 1, 12);
        }
    }

    [Fact]
    public void GetRandomGenericBackdrop_CalledMultipleTimes_CanReturnDifferentValues()
    {
        // Arrange
        BackdropPicture sut = CreateSut();
        HashSet<string> results = [];

        // Act
        for (int i = 0; i < 50; i++)
        {
            results.Add(sut.GetRandomGenericBackdrop());
        }

        // Assert
        Assert.True(results.Count > 1);
    }

    [Fact]
    public void GetArtistPictureFolder_DifferentArtists_ReturnsDifferentPaths()
    {
        // Arrange
        BackdropPicture sut = CreateSut();

        // Act
        string path1 = sut.GetArtistPictureFolder("Artist One");
        string path2 = sut.GetArtistPictureFolder("Artist Two");

        // Assert
        Assert.NotEqual(path1, path2);
        Assert.Contains("Artist One", path1);
        Assert.Contains("Artist Two", path2);
    }
}