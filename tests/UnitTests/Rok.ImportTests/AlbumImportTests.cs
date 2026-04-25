using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;
using Rok.Import.Models;

namespace Rok.ImportTests;

public class AlbumImportTests
{
    [Fact(DisplayName = "GetFromCache should return null for empty album name")]
    public void GetFromCache_ShouldReturnNull_ForEmptyAlbumName()
    {
        // Arrange
        AlbumImport import = new(Mock.Of<IAlbumRepository>());

        // Act
        AlbumCacheItem? result = import.GetFromCache("", false, 1);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "LoadCache should populate cache with albums from repository")]
    public async Task LoadCache_ShouldPopulateCache_WithAlbumsFromRepository()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, Name = "Thriller", ArtistId = 10, IsCompilation = false },
            new() { Id = 2, Name = "Now That's What I Call Music", IsCompilation = true }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        AlbumImport import = new(repository.Object);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(2, import.CountInCache);
        Assert.NotNull(import.GetFromCache("Thriller", false, 10));
        Assert.NotNull(import.GetFromCache("Now That's What I Call Music", true, null));
    }

    [Fact(DisplayName = "GetFromCache should differentiate same album name by artist when not a compilation")]
    public async Task GetFromCache_ShouldDifferentiateSameAlbumName_ByArtist_WhenNotCompilation()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, Name = "Greatest", ArtistId = 10, IsCompilation = false },
            new() { Id = 2, Name = "Greatest", ArtistId = 20, IsCompilation = false }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        AlbumImport import = new(repository.Object);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? forArtist10 = import.GetFromCache("Greatest", false, 10);
        AlbumCacheItem? forArtist20 = import.GetFromCache("Greatest", false, 20);

        // Assert
        Assert.Equal(1, forArtist10!.Id);
        Assert.Equal(2, forArtist20!.Id);
    }

    [Fact(DisplayName = "CreateAsync should return null when track has no album name")]
    public async Task CreateAsync_ShouldReturnNull_WhenTrackHasNoAlbumName()
    {
        // Arrange
        AlbumImport import = new(Mock.Of<IAlbumRepository>());

        // Act
        AlbumCacheItem? result = await import.CreateAsync(new TrackFile { Album = "", FullPath = @"C:\music\x.mp3" }, null, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "CreateAsync should return null when track has no file path")]
    public async Task CreateAsync_ShouldReturnNull_WhenTrackHasNoFilePath()
    {
        // Arrange
        AlbumImport import = new(Mock.Of<IAlbumRepository>());

        // Act
        AlbumCacheItem? result = await import.CreateAsync(new TrackFile { Album = "A", FullPath = "" }, null, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "CreateAsync should flag album as live when name matches live keyword")]
    public async Task CreateAsync_ShouldFlagAlbumAsLive_WhenNameMatchesLiveKeyword()
    {
        // Arrange
        AlbumEntity? captured = null;
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<AlbumEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        AlbumImport import = new(repository.Object);

        // Act
        await import.CreateAsync(new TrackFile { Album = "Unplugged In New York live", FullPath = @"C:\music\x.mp3" }, 10, null);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.IsLive);
    }

    [Fact(DisplayName = "CreateAsync should flag album as best of when name matches best of mask")]
    public async Task CreateAsync_ShouldFlagAlbumAsBestOf_WhenNameMatchesBestOfMask()
    {
        // Arrange
        AlbumEntity? captured = null;
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<AlbumEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        AlbumImport import = new(repository.Object);

        // Act
        await import.CreateAsync(new TrackFile { Album = "The Greatest Hits", FullPath = @"C:\music\x.mp3" }, 10, null);

        // Assert
        Assert.True(captured!.IsBestOf);
    }

    [Fact(DisplayName = "CreateAsync should derive album path from track file full path")]
    public async Task CreateAsync_ShouldDeriveAlbumPath_FromTrackFullPath()
    {
        // Arrange
        AlbumEntity? captured = null;
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<AlbumEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        AlbumImport import = new(repository.Object);

        // Act
        await import.CreateAsync(new TrackFile { Album = "My Album", FullPath = @"C:\music\artist\album\track.mp3" }, 7, 3);

        // Assert
        Assert.Equal(@"C:\music\artist\album", captured!.AlbumPath);
        Assert.Equal(7, captured.ArtistId);
        Assert.Equal(3, captured.GenreId);
    }

    [Fact(DisplayName = "CreateAsync should increment created count and add item to cache")]
    public async Task CreateAsync_ShouldIncrementCreatedCount_AndAddItemToCache()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(99);
        AlbumImport import = new(repository.Object);

        // Act
        AlbumCacheItem? created = await import.CreateAsync(new TrackFile { Album = "ok", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(99, created!.Id);
        Assert.Equal(1, import.CreatedCount);
        Assert.Equal(1, import.CountInCache);
    }
}
