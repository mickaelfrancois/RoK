using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;

namespace Rok.ImportTests;

public class TrackImportTests
{
    [Fact(DisplayName = "GetFromCache should return null for empty music file path")]
    public void GetFromCache_ShouldReturnNull_ForEmptyMusicFilePath()
    {
        // Arrange
        TrackImport import = new(Mock.Of<ITrackRepository>());

        // Act & Assert
        Assert.Null(import.GetFromCache(""));
    }

    [Fact(DisplayName = "LoadCache should index tracks case-insensitively by music file path")]
    public async Task LoadCache_ShouldIndexTracks_CaseInsensitively_ByMusicFilePath()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Title = "t1", MusicFile = @"C:\music\Song.mp3" }
        };
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        TrackImport import = new(repository.Object);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(1, import.CountInCache);
        Assert.NotNull(import.GetFromCache(@"c:\music\song.mp3"));
    }

    [Fact(DisplayName = "CreateAsync should add track to repository and store it in the cache")]
    public async Task CreateAsync_ShouldAddTrackToRepository_AndStoreItInCache()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        TrackImport import = new(repository.Object);
        TrackEntity track = new() { Id = 1, Title = "new", MusicFile = @"C:\music\new.mp3" };

        // Act
        TrackEntity? created = await import.CreateAsync(track);

        // Assert
        Assert.Same(track, created);
        Assert.Equal(1, import.CreatedCount);
        Assert.Same(track, import.GetFromCache(@"C:\music\new.mp3"));
    }

    [Fact(DisplayName = "UpdateTrackAsync should delegate to repository UpdateAsync")]
    public async Task UpdateTrackAsync_ShouldDelegateToRepository_UpdateAsync()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        TrackImport import = new(repository.Object);
        TrackEntity track = new() { Id = 2, Title = "updated" };

        // Act
        await import.UpdateTrackAsync(track);

        // Assert
        repository.Verify(r => r.UpdateAsync(track, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateTrackFileDateAsync should forward the call to the repository")]
    public async Task UpdateTrackFileDateAsync_ShouldForwardCall_ToRepository()
    {
        // Arrange
        Mock<ITrackRepository> repository = new();
        TrackImport import = new(repository.Object);
        DateTime fileDate = new(2024, 5, 1);

        // Act
        await import.UpdateTrackFileDateAsync(7, fileDate);

        // Assert
        repository.Verify(r => r.UpdateFileDateAsync(7, fileDate, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateTrackFileDateAsync should throw when track id is not greater than zero")]
    public Task UpdateTrackFileDateAsync_ShouldThrow_WhenTrackIdIsNotGreaterThanZero()
    {
        // Arrange
        TrackImport import = new(Mock.Of<ITrackRepository>());

        // Act & Assert
        return Assert.ThrowsAsync<ArgumentException>(() => import.UpdateTrackFileDateAsync(0, DateTime.UtcNow));
    }
}
