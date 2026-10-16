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
        ArtistImport import = new(Mock.Of<IArtistRepository>(), TimeProvider.System);

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
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(1, import.CountInCache);
        Assert.NotNull(import.GetFromCache("DAFT PUNK"));
        Assert.NotNull(import.GetFromCache("daft punk"));
    }

    [Fact(DisplayName = "load_cache_registers_the_pending_rename_as_an_alias")]
    public async Task LoadCache_PendingRename_RegistersTheOldNameAsAlias()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 7, Name = "AC/DC", RenamePendingFrom = "Ac/dc", GenreId = 5 },
            new() { Id = 8, Name = "Daft Punk" }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(7, import.GetFromCache("Ac/dc")!.Id);
        Assert.Equal(7, import.GetFromCache("AC/DC")!.Id);
        Assert.Equal(8, import.GetFromCache("Daft Punk")!.Id);
    }

    [Fact(DisplayName = "load_cache_alias_never_shadows_an_existing_artist_name")]
    public async Task LoadCache_AliasEqualToAnotherArtist_KeepsTheRealArtist()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 7, Name = "New Name", RenamePendingFrom = "Other" },
            new() { Id = 8, Name = "Other" }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(8, import.GetFromCache("Other")!.Id);
        Assert.Equal(7, import.GetFromCache("New Name")!.Id);
    }

    [Fact(DisplayName = "CreateAsync should return null when track artist name is empty")]
    public async Task CreateAsync_ShouldReturnNull_WhenTrackArtistIsEmpty()
    {
        // Arrange
        ArtistImport import = new(Mock.Of<IArtistRepository>(), TimeProvider.System);

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
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync(new TrackFile { Artist = "queen", FullPath = @"C:\m\t.mp3", IsCompilation = false }, 7);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(1, captured!.AlbumCount);
        Assert.Equal(0, captured.CompilationCount);
        Assert.Equal(7, captured.GenreId);
    }

    [Fact(DisplayName = "create_keeps_the_tag_casing")]
    public async Task CreateAsync_ShouldKeepTagCasing()
    {
        // Arrange
        ArtistEntity? captured = null;
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<ArtistEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        ArtistCacheItem? created = await import.CreateAsync(new TrackFile { Artist = "AC/DC", FullPath = @"C:\m\t.mp3" }, null);

        // Assert
        Assert.Equal("AC/DC", captured!.Name);
        Assert.Equal("AC/DC", created!.Name);
    }

    [Fact(DisplayName = "a_case_variant_of_an_existing_artist_maps_to_it")]
    public async Task GetFromCache_ShouldMapCaseVariant_ToExistingArtist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<ArtistEntity> { new() { Id = 3, Name = "Ac/dc" } });
        ArtistImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        ArtistCacheItem? hit = import.GetFromCache("ac/dc");

        // Assert
        Assert.Equal(3, hit!.Id);
        repository.Verify(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "two_case_variants_in_one_import_resolve_to_one_artist")]
    public async Task GetFromCache_ShouldResolveSecondCaseVariant_ToTheCreatedArtist()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync(new TrackFile { Artist = "AC/DC", FullPath = @"C:\m\t.mp3" }, null);
        ArtistCacheItem? second = import.GetFromCache("Ac/DC");

        // Assert
        Assert.Equal(1, second!.Id);
        repository.Verify(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Once);
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
        ArtistImport import = new(repository.Object, TimeProvider.System);

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
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        ArtistCacheItem? created = await import.CreateAsync(new TrackFile { Artist = "Metallica", FullPath = @"C:\m\t.mp3" }, 2);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(50, created!.Id);
        Assert.Equal(1, import.CreatedCount);
        Assert.NotNull(import.GetFromCache("Metallica"));
    }

    [Fact(DisplayName = "NewlyCreatedIds should be empty on initialization")]
    public void NewlyCreatedIds_ShouldBeEmpty_OnInitialization()
    {
        // Arrange & Act
        ArtistImport import = new(Mock.Of<IArtistRepository>(), TimeProvider.System);

        // Assert
        Assert.Empty(import.NewlyCreatedIds);
    }

    [Fact(DisplayName = "CreateAsync should add the new artist id to NewlyCreatedIds")]
    public async Task CreateAsync_ShouldAddIdToNewlyCreatedIds()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(42L);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync(new TrackFile { Artist = "Metallica", FullPath = @"C:\m\t.mp3" }, null);

        // Assert
        Assert.Single(import.NewlyCreatedIds);
        Assert.Equal(42L, import.NewlyCreatedIds[0]);
    }

    [Fact(DisplayName = "CreateAsync should not add more than 100 ids to NewlyCreatedIds")]
    public async Task CreateAsync_ShouldNotAddMoreThan100Ids_ToNewlyCreatedIds()
    {
        // Arrange
        long id = 1;
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(() => id++);
        ArtistImport import = new(repository.Object, TimeProvider.System);

        // Act
        for (int i = 0; i < 101; i++)
            await import.CreateAsync(new TrackFile { Artist = $"Artist{i}", FullPath = @"C:\m\t.mp3" }, null);

        // Assert
        Assert.Equal(100, import.NewlyCreatedIds.Count);
    }

    [Fact(DisplayName = "GetFromCache should find existing artist when stored name uses unicode hyphen and lookup uses ascii hyphen")]
    public async Task GetFromCache_ShouldFindExistingArtist_WhenStoredNameUsesUnicodeHyphen_AndLookupUsesAsciiHyphen()
    {
        // Arrange — stored "Pro<U+2010>pain", lookup with ascii hyphen
        List<ArtistEntity> artists = new()
        {
            new() { Id = 42, Name = "Pro‐pain" }
        };
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        ArtistImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        ArtistCacheItem? hit = import.GetFromCache("Pro-pain");

        // Assert
        Assert.NotNull(hit);
        Assert.Equal(42, hit!.Id);
    }

    [Fact(DisplayName = "LoadCacheAsync should clear NewlyCreatedIds")]
    public async Task LoadCacheAsync_ShouldClearNewlyCreatedIds()
    {
        // Arrange
        Mock<IArtistRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1L);
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<ArtistEntity>());
        ArtistImport import = new(repository.Object, TimeProvider.System);
        await import.CreateAsync(new TrackFile { Artist = "Metallica", FullPath = @"C:\m\t.mp3" }, null);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Empty(import.NewlyCreatedIds);
    }
}