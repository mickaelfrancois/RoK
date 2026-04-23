using Moq;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests.Files;

public class ArtistPictureTests
{
    private const string CachePath = "/cache";
    private const string ArtistName = "Test Artist";
    private const string ArtistFolderName = "Test Artist";
    private static string RepositoryArtistPath => Path.Combine(CachePath, "@Artists");
    private static string ArtistFolder => Path.Combine(RepositoryArtistPath, ArtistFolderName);
    private static string ArtistPictureFile => Path.Combine(ArtistFolder, "artist.jpg");

    private static ArtistPicture CreateSut(Mock<IFileSystem> fileSystem)
    {
        Mock<IAppOptions> options = new();
        options.Setup(o => o.CachePath).Returns(CachePath);
        return new ArtistPicture(fileSystem.Object, options.Object);
    }

    [Fact]
    public void Constructor_CreatesRepositoryFolder()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(RepositoryArtistPath));
        Mock<IAppOptions> options = new();
        options.Setup(o => o.CachePath).Returns(CachePath);

        // Act
        ArtistPicture sut = new(fs.Object, options.Object);

        // Assert
        Assert.Equal(RepositoryArtistPath, sut.RepositoryArtistPath);
        fs.Verify(f => f.DirectoryCreate(RepositoryArtistPath), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void SetRepositoryArtistPath_UpdatesPathAndCreatesFolder()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);
        string newPath = "/new/cache";
        string expectedRepoPath = Path.Combine(newPath, "@Artists");

        // Act
        sut.SetRepositoryArtistPath(newPath);

        // Assert
        Assert.Equal(expectedRepoPath, sut.RepositoryArtistPath);
        fs.Verify(f => f.DirectoryCreate(RepositoryArtistPath), Times.Once);
        fs.Verify(f => f.DirectoryCreate(expectedRepoPath), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void SetRepositoryArtistPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.SetRepositoryArtistPath(""));
        Assert.Throws<ArgumentNullException>(() => sut.SetRepositoryArtistPath(null!));
    }

    [Fact]
    public void GetArtistFolder_ReturnsCorrectPath()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act
        string result = sut.GetArtistFolder(ArtistName);

        // Assert
        Assert.Equal(ArtistFolder, result);
    }

    [Fact]
    public void GetArtistFolder_WithSpecialCharacters_SanitizesName()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);
        string artistNameWithSpecialChars = "AC/DC: The Band";

        // Act
        string result = sut.GetArtistFolder(artistNameWithSpecialChars);

        result = result.Substring(CachePath.Length);
        // Assert
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain(":", result);
        Assert.Contains("@Artists", result);
    }

    [Fact]
    public void GetArtistFolder_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetArtistFolder(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetArtistFolder(null!));
    }

    [Fact]
    public void GetPictureFile_ReturnsCorrectPath()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act
        string result = sut.GetPictureFile(ArtistName);

        // Assert
        Assert.Equal(ArtistPictureFile, result);
        Assert.EndsWith("artist.jpg", result);
    }

    [Fact]
    public void GetPictureFile_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetPictureFile(""));
        Assert.Throws<ArgumentNullException>(() => sut.GetPictureFile(null!));
    }

    [Fact]
    public void PictureFileExists_FileExists_ReturnsTrue()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        fs.Setup(f => f.FileExists(ArtistPictureFile)).Returns(true);
        ArtistPicture sut = CreateSut(fs);

        // Act
        bool exists = sut.PictureFileExists(ArtistName);

        // Assert
        Assert.True(exists);
        fs.Verify(f => f.FileExists(ArtistPictureFile), Times.Once);
    }

    [Fact]
    public void PictureFileExists_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        fs.Setup(f => f.FileExists(ArtistPictureFile)).Returns(false);
        ArtistPicture sut = CreateSut(fs);

        // Act
        bool exists = sut.PictureFileExists(ArtistName);

        // Assert
        Assert.False(exists);
        fs.Verify(f => f.FileExists(ArtistPictureFile), Times.Once);
    }

    [Fact]
    public void PictureFileExists_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.PictureFileExists(""));
        Assert.Throws<ArgumentNullException>(() => sut.PictureFileExists(null!));
    }

    [Fact]
    public void GetPictureFile_DifferentArtists_ReturnsDifferentPaths()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(It.IsAny<string>()));
        ArtistPicture sut = CreateSut(fs);

        // Act
        string path1 = sut.GetPictureFile("Artist One");
        string path2 = sut.GetPictureFile("Artist Two");

        // Assert
        Assert.NotEqual(path1, path2);
        Assert.Contains("Artist One", path1);
        Assert.Contains("Artist Two", path2);
    }
}