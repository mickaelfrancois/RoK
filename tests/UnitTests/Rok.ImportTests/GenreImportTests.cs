using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;
using Rok.Import.Models;

namespace Rok.ImportTests;

public class GenreImportTests
{
    [Fact(DisplayName = "GetFromCache should return null for empty genre name")]
    public void GetFromCache_ShouldReturnNull_ForEmptyGenreName()
    {
        // Arrange
        GenreImport import = new(Mock.Of<IGenreRepository>(), TimeProvider.System);

        // Act & Assert
        Assert.Null(import.GetFromCache(""));
    }

    [Fact(DisplayName = "LoadCache should load genres case-insensitively from repository")]
    public async Task LoadCache_ShouldLoadGenres_CaseInsensitively_FromRepository()
    {
        // Arrange
        List<GenreEntity> genres = new() { new() { Id = 1, Name = "Rock" }, new() { Id = 2, Name = "Jazz" } };
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres);
        GenreImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(2, import.CountInCache);
        Assert.NotNull(import.GetFromCache("ROCK"));
        Assert.NotNull(import.GetFromCache("jazz"));
    }

    [Fact(DisplayName = "CreateAsync should return null for empty genre name")]
    public async Task CreateAsync_ShouldReturnNull_ForEmptyGenreName()
    {
        // Arrange
        GenreImport import = new(Mock.Of<IGenreRepository>(), TimeProvider.System);

        // Act
        GenreCacheItem? result = await import.CreateAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "CreateAsync should persist capitalized genre name with artist count seeded at one")]
    public async Task CreateAsync_ShouldPersistCapitalizedGenreName_WithArtistCountSeededAtOne()
    {
        // Arrange
        GenreEntity? captured = null;
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<GenreEntity, RepositoryConnectionKind>((g, _) => captured = g)
            .ReturnsAsync(7);
        GenreImport import = new(repository.Object, TimeProvider.System);

        // Act
        GenreCacheItem? created = await import.CreateAsync("electro");

        // Assert
        Assert.NotNull(created);
        Assert.Equal(7, created!.Id);
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.ArtistCount);
        Assert.Equal(1, import.CreatedCount);
        Assert.NotNull(import.GetFromCache("electro"));
    }
}
