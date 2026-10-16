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

    [Fact(DisplayName = "GetFromCache should find existing genre when stored name uses unicode hyphen and lookup uses ascii hyphen")]
    public async Task GetFromCache_ShouldFindExistingGenre_WhenStoredNameUsesUnicodeHyphen_AndLookupUsesAsciiHyphen()
    {
        // Arrange
        List<GenreEntity> genres = new() { new() { Id = 9, Name = "Hip‐hop" } };
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres);
        GenreImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        GenreCacheItem? hit = import.GetFromCache("Hip-hop");

        // Assert
        Assert.NotNull(hit);
        Assert.Equal(9, hit!.Id);
    }

    [Fact(DisplayName = "CreateAsync should persist the genre name as tagged with artist count seeded at one")]
    public async Task CreateAsync_ShouldPersistGenreName_AsTagged_WithArtistCountSeededAtOne()
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
        Assert.Equal("electro", captured!.Name);
        Assert.Equal(1, captured.ArtistCount);
        Assert.Equal(1, import.CreatedCount);
        Assert.NotNull(import.GetFromCache("electro"));
    }

    [Fact(DisplayName = "create_keeps_the_tag_casing")]
    public async Task CreateAsync_ShouldKeepTagCasing()
    {
        // Arrange
        GenreEntity? captured = null;
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<GenreEntity, RepositoryConnectionKind>((g, _) => captured = g)
            .ReturnsAsync(1);
        GenreImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync("R&B/Soul");

        // Assert
        Assert.Equal("R&B/Soul", captured!.Name);
    }

    [Fact(DisplayName = "a_case_variant_of_an_existing_genre_maps_to_it")]
    public async Task GetFromCache_ShouldMapCaseVariant_ToExistingGenre()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<GenreEntity> { new() { Id = 4, Name = "R&B/Soul" } });
        GenreImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        GenreCacheItem? hit = import.GetFromCache("r&b/soul");

        // Assert
        Assert.Equal(4, hit!.Id);
        repository.Verify(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "two_case_variants_in_one_import_resolve_to_one_genre")]
    public async Task GetFromCache_ShouldResolveSecondCaseVariant_ToTheCreatedGenre()
    {
        // Arrange
        Mock<IGenreRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        GenreImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync("Trip-Hop");
        GenreCacheItem? second = import.GetFromCache("trip-hop");

        // Assert
        Assert.Equal(1, second!.Id);
        repository.Verify(r => r.AddAsync(It.IsAny<GenreEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }
}