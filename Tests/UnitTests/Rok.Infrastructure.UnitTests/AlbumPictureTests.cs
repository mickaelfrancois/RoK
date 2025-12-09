using Moq;
using Rok.Application.Interfaces;
using Rok.Infrastructure.Files;

namespace Rok.Infrastructure.UnitTests;

public class AlbumPictureTests
{
    private const string BasePath = "/music/album";
    private static string CoverJpg => Path.Join(BasePath, "cover.jpg");
    private static string CoverPng => Path.Join(BasePath, "cover.png");
    private static string FolderJpg => Path.Join(BasePath, "folder.jpg");
    private static string CoverWebp => Path.Join(BasePath, "cover.webp");
    private static string FolderWebp => Path.Join(BasePath, "folder.webp");

    [Fact]
    public void GetPictureFile_NoFiles_ReturnsEmpty_QueriesAllInOrder()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        MockSequence seq = new();
        fs.InSequence(seq).Setup(f => f.FileExists(CoverJpg)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(CoverPng)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(FolderJpg)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(CoverWebp)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(FolderWebp)).Returns(false);
        AlbumPicture sut = new(fs.Object);

        // Act
        string result = sut.GetPictureFile(BasePath);

        // Assert
        Assert.Equal("/music/album\\cover.jpg", result);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Once);
        fs.Verify(f => f.FileExists(CoverPng), Times.Once);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Once);
        fs.Verify(f => f.FileExists(CoverWebp), Times.Once);
        fs.Verify(f => f.FileExists(FolderWebp), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetPictureFile_FirstCandidateFound_StopsEarly()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.FileExists(CoverJpg)).Returns(true);
        AlbumPicture sut = new(fs.Object);

        // Act
        string path = sut.GetPictureFile(BasePath);

        // Assert
        Assert.Equal(CoverJpg, path);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Once);
        // Ensures no further calls
        fs.Verify(f => f.FileExists(CoverPng), Times.Never);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Never);
        fs.VerifyNoOtherCalls();
    }


    [Fact]
    public void GetPictureFile_SecondCandidateFound_WhenFirstMissing()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        MockSequence seq = new();
        fs.InSequence(seq).Setup(f => f.FileExists(CoverJpg)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(CoverPng)).Returns(true);
        AlbumPicture sut = new(fs.Object);

        // Act
        string path = sut.GetPictureFile(BasePath);

        // Assert
        Assert.Equal(CoverPng, path);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Once);
        fs.Verify(f => f.FileExists(CoverPng), Times.Once);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Never);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void GetPictureFile_ThirdCandidateFound_WhenFirstTwoMissing()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        MockSequence seq = new();
        fs.InSequence(seq).Setup(f => f.FileExists(CoverJpg)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(CoverPng)).Returns(false);
        fs.InSequence(seq).Setup(f => f.FileExists(FolderJpg)).Returns(true);
        AlbumPicture sut = new(fs.Object);

        // Act
        string path = sut.GetPictureFile(BasePath);

        // Assert
        Assert.Equal(FolderJpg, path);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Once);
        fs.Verify(f => f.FileExists(CoverPng), Times.Once);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void PictureFileExists_NoFile_ReturnsFalse()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.FileExists(CoverJpg)).Returns(false);
        fs.Setup(f => f.FileExists(CoverPng)).Returns(false);
        fs.Setup(f => f.FileExists(FolderJpg)).Returns(false);
        fs.Setup(f => f.FileExists(CoverWebp)).Returns(false);
        fs.Setup(f => f.FileExists(FolderWebp)).Returns(false);
        AlbumPicture sut = new(fs.Object);

        // Act
        bool exists = sut.PictureFileExists(BasePath);

        // Assert
        Assert.False(exists);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Exactly(2));
        fs.Verify(f => f.FileExists(CoverPng), Times.Once);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Once);
        fs.Verify(f => f.FileExists(CoverWebp), Times.Once);
        fs.Verify(f => f.FileExists(FolderWebp), Times.Once);
        fs.VerifyNoOtherCalls();
    }

    [Fact]
    public void PictureFileExists_FileFound_ReturnsTrue_AndShortCircuits()
    {
        // Arrange
        Mock<IFileSystem> fs = new(MockBehavior.Strict);
        fs.Setup(f => f.FileExists(CoverJpg)).Returns(true);
        AlbumPicture sut = new(fs.Object);

        // Act
        bool exists = sut.PictureFileExists(BasePath);

        // Assert
        Assert.True(exists);
        fs.Verify(f => f.FileExists(CoverJpg), Times.Exactly(2));
        fs.Verify(f => f.FileExists(CoverPng), Times.Never);
        fs.Verify(f => f.FileExists(FolderJpg), Times.Never);
        fs.VerifyNoOtherCalls();
    }
}
