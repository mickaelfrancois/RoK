using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;
using Rok.Import.Models;

namespace Rok.ImportTests;

public class ArtistImportTests
{
    [Fact(DisplayName = "GetFromCache should return null for empty artist name")]
    public void GetFromCache_ShouldReturnNull_ForEmptyArtistName()
    {
        // Arrange
        ArtistImport import = new(Mock.Of<IArtistRepository>());

        // Act & Assert
        Assert.Null(import.GetFromCache(""));
    }

    [Fact(DisplayName = "LoadCache should populate the cache case-insensitively")]
    public async Task LoadCache_ShouldPopulateCache_CaseInsensitively()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, Name = "Daft Punk", GenreId = 5 }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        ArtistImport import = new(repository.Object);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(1, import.CountInCache);
        Assert.NotNull(import.GetFromCache("DAFT PUNK"));
        Assert.NotNull(import.GetFromCache("daft punk"));
    }

    [Fact(DisplayName = "CreateAsync should return null when track artist name is empty")]
    public async Task CreateAsync_ShouldReturnNull_WhenTrackArtistIsEmpty()
    {
        // Arrange
        ArtistImport import = new(Mock.Of<IArtistRepository>());

        // Act
        ArtistCacheItem? result = await import.CreateAsync(new TrackFile { Artist = "", FullPath = @"C:\music\x.mp3" }, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "CreateAsync should set album count for non-compilation tracks")]
    public async Task CreateAsync_ShouldSetAlbumCount_ForNonCompilationTracks()
    {
        // Arrange
        ArtistEntity? captured = null;
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ArtistEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        ArtistImport import = new(repository.Object);

        // Act
        await import.CreateAsync(new TrackFile { Artist = "queen", FullPath = @"C:\m\t.mp3", IsCompilation = false }, 7);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.AlbumCount);
        Assert.Equal(0, captured.CompilationCount);
        Assert.Equal(7, captured.GenreId);
    }

    [Fact(DisplayName = "CreateAsync should set compilation count when track is a compilation")]
    public async Task CreateAsync_ShouldSetCompilationCount_WhenTrackIsCompilation()
    {
        // Arrange
        ArtistEntity? captured = null;
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ArtistEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        ArtistImport import = new(repository.Object);

        // Act
        await import.CreateAsync(new TrackFile { Artist = "various", FullPath = @"C:\m\t.mp3", IsCompilation = true }, null);

        // Assert
        Assert.Equal(0, captured!.AlbumCount);
        Assert.Equal(1, captured.CompilationCount);
    }

    [Fact(DisplayName = "CreateAsync should increment created count and cache the new artist")]
    public async Task CreateAsync_ShouldIncrementCreatedCount_AndCacheNewArtist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(50);
        ArtistImport import = new(repository.Object);

        // Act
        ArtistCacheItem? created = await import.CreateAsync(new TrackFile { Artist = "Metallica", FullPath = @"C:\m\t.mp3" }, 2);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(50, created!.Id);
        Assert.Equal(1, import.CreatedCount);
        Assert.NotNull(import.GetFromCache("Metallica"));
    }
}
