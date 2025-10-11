using Moq;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests;

public class ArtistPictureTests
{
    private const string CacheRoot = "/cache";
    private const string ArtistsFolderName = "@Artists";
    private static string RepositoryArtistPath => Path.Combine(CacheRoot, ArtistsFolderName);
    private static string ArtistName => "ArtistA";
    private static string ArtistFolder => Path.Combine(RepositoryArtistPath, ArtistName);
    private static string ArtistPictureFile => Path.Combine(ArtistFolder, ArtistPicture.KArtistFileName);

    private static ArtistPicture CreateSut(Mock<IFileSystem> fs, Mock<IAppOptions> options)
        => new(fs.Object, options.Object);


    [Fact]
    public void GetArtistFolder_Null_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.GetArtistFolder(null!));
    }

    [Fact]
    public void GetArtistFolder_Empty_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetArtistFolder(""));
    }

    [Fact]
    public void GetArtistFolder_ReturnsExpectedPath()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act
        string folder = sut.GetArtistFolder(ArtistName);

        // Assert
        Assert.Equal(ArtistFolder, folder);
    }

    [Fact]
    public void GetPictureFile_Null_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.GetPictureFile(null!));
    }

    [Fact]
    public void GetPictureFile_Empty_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.GetPictureFile(""));
    }

    [Fact]
    public void GetPictureFile_ReturnsExpectedFullPath()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();


        // Act
        string file = sut.GetPictureFile(ArtistName);

        // Assert
        Assert.Equal(ArtistPictureFile, file);
    }

    [Fact]
    public void PictureFileExists_FilePresent_ReturnsTrue()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(RepositoryArtistPath));
        fs.Setup(f => f.FileExists(ArtistPictureFile)).Returns(true);

        Mock<IAppOptions> opts = new(MockBehavior.Strict);
        opts.SetupProperty(o => o.CachePath, CacheRoot);

        ArtistPicture sut = CreateSut(fs, opts);

        // Act
        bool exists = sut.PictureFileExists(ArtistName);

        // Assert
        Assert.True(exists);
        fs.Verify(f => f.FileExists(ArtistPictureFile), Times.Once);
        fs.Verify(f => f.DirectoryCreate(RepositoryArtistPath), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void PictureFileExists_FileMissing_ReturnsFalse()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(RepositoryArtistPath));
        fs.Setup(f => f.FileExists(ArtistPictureFile)).Returns(false);

        Mock<IAppOptions> opts = new(MockBehavior.Strict);
        opts.SetupProperty(o => o.CachePath, CacheRoot);

        ArtistPicture sut = CreateSut(fs, opts);

        // Act
        bool exists = sut.PictureFileExists(ArtistName);

        // Assert
        Assert.False(exists);
        fs.Verify(f => f.FileExists(ArtistPictureFile), Times.Once);
        fs.Verify(f => f.DirectoryCreate(RepositoryArtistPath), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void PictureFileExists_Null_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.PictureFileExists(null!));
    }

    [Fact]
    public void PictureFileExists_Empty_Throws()
    {
        // Arrange
        ArtistPicture sut = MakeDefaultSut();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.PictureFileExists(""));
    }

    // Helper to create a default SUT with common setup
    private static ArtistPicture MakeDefaultSut()
    {
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.DirectoryCreate(RepositoryArtistPath));

        Mock<IAppOptions> opts = new(MockBehavior.Strict);
        opts.SetupProperty(o => o.CachePath, CacheRoot);

        return new ArtistPicture(fs.Object, opts.Object);
    }
}