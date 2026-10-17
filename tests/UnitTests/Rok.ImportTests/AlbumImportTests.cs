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
        AlbumImport import = new(Mock.Of<IAlbumRepository>(), TimeProvider.System);

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
        AlbumImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Equal(2, import.CountInCache);
        Assert.NotNull(import.GetFromCache("Thriller", false, 10));
        Assert.NotNull(import.GetFromCache("Now That's What I Call Music", true, null));
    }

    [Fact(DisplayName = "GetFromCache should find existing album when stored name uses unicode hyphen and lookup uses ascii hyphen")]
    public async Task GetFromCache_ShouldFindExistingAlbum_WhenStoredNameUsesUnicodeHyphen_AndLookupUsesAsciiHyphen()
    {
        // Arrange — non compilation, scoped by artistId
        List<AlbumEntity> albums = new()
        {
            new() { Id = 77, Name = "Foul Taste of Freedom – the album", ArtistId = 42, IsCompilation = false }
        };
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act — same album name but lookup uses ASCII hyphen
        AlbumCacheItem? hit = import.GetFromCache("Foul Taste of Freedom - the album", false, 42);

        // Assert
        Assert.NotNull(hit);
        Assert.Equal(77, hit!.Id);
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
        AlbumImport import = new(repository.Object, TimeProvider.System);
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
        AlbumImport import = new(Mock.Of<IAlbumRepository>(), TimeProvider.System);

        // Act
        AlbumCacheItem? result = await import.CreateAsync(new TrackFile { Album = "", FullPath = @"C:\music\x.mp3" }, null, null);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "CreateAsync should return null when track has no file path")]
    public async Task CreateAsync_ShouldReturnNull_WhenTrackHasNoFilePath()
    {
        // Arrange
        AlbumImport import = new(Mock.Of<IAlbumRepository>(), TimeProvider.System);

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
        AlbumImport import = new(repository.Object, TimeProvider.System);

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
        AlbumImport import = new(repository.Object, TimeProvider.System);

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
        AlbumImport import = new(repository.Object, TimeProvider.System);

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
        AlbumImport import = new(repository.Object, TimeProvider.System);

        // Act
        AlbumCacheItem? created = await import.CreateAsync(new TrackFile { Album = "ok", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(99, created!.Id);
        Assert.Equal(1, import.CreatedCount);
        Assert.Equal(1, import.CountInCache);
    }

    [Fact(DisplayName = "create_keeps_the_tag_casing")]
    public async Task CreateAsync_ShouldKeepTagCasing()
    {
        // Arrange
        AlbumEntity? captured = null;
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .Callback<AlbumEntity, RepositoryConnectionKind>((a, _) => captured = a)
            .ReturnsAsync(1);
        AlbumImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync(new TrackFile { Album = "Back In Black (AC/DC)", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Assert
        Assert.Equal("Back In Black (AC/DC)", captured!.Name);
    }

    [Fact(DisplayName = "a_case_variant_of_an_existing_album_maps_to_it")]
    public async Task GetFromCache_ShouldMapCaseVariant_ToExistingAlbum()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity> { new() { Id = 8, Name = "Back In Black", ArtistId = 10, IsCompilation = false } });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? hit = import.GetFromCache("back in black", false, 10);

        // Assert
        Assert.Equal(8, hit!.Id);
        repository.Verify(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "get_by_id_returns_loaded_and_created_albums")]
    public async Task GetById_ShouldReturnLoadedAndCreatedAlbums()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity> { new() { Id = 8, Name = "Back In Black", ArtistId = 10, GenreId = 4 } });
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(99L);
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? created = await import.CreateAsync(new TrackFile { Album = "Other", FullPath = @"C:\m\t.mp3" }, 1, 7);

        // Assert
        Assert.Same(import.GetFromCache("Back In Black", false, 10), import.GetById(8));
        Assert.Equal(4, import.GetById(8)!.GenreId);
        Assert.Same(created, import.GetById(99));
        Assert.Equal(7, import.GetById(99)!.GenreId);
        Assert.Null(import.GetById(1234));
    }

    [Fact(DisplayName = "update_compilation_rekeys_the_cache")]
    public async Task UpdateCompilationAsync_ShouldRekeyTheCache()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity> { new() { Id = 8, Name = "Mix", IsCompilation = true, ArtistId = 10 } });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();
        AlbumCacheItem album = import.GetById(8)!;

        // Act
        await import.UpdateCompilationAsync(album, false, 20, Rok.Domain.Enums.ECompilationSource.Derived);

        // Assert
        Assert.Null(import.GetFromCache("Mix", true, null));
        Assert.Same(album, import.GetFromCache("Mix", false, 20));
        Assert.False(album.IsCompilation);
        Assert.Equal(20, album.ArtistId);
        Assert.Same(album, import.GetById(8));
        repository.Verify(r => r.UpdateCompilationAsync(8, false, 20, Rok.Domain.Enums.ECompilationSource.Derived, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "update_compilation_never_steals_another_album_key")]
    public async Task UpdateCompilationAsync_ShouldNeverStealAnotherAlbumKey()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity>
            {
                new() { Id = 8, Name = "Mix", IsCompilation = true },
                new() { Id = 9, Name = "Mix", IsCompilation = false, ArtistId = 20 }
            });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();
        AlbumCacheItem converted = import.GetById(8)!;

        // Act
        await import.UpdateCompilationAsync(converted, false, 20, Rok.Domain.Enums.ECompilationSource.Derived);

        // Assert
        Assert.Equal(9, import.GetFromCache("Mix", false, 20)!.Id);
        Assert.Null(import.GetFromCache("Mix", true, null));
        Assert.Equal(8, import.GetById(8)!.Id);
    }

    [Fact(DisplayName = "update_compilation_keeps_the_old_key_when_it_points_to_another_album")]
    public async Task UpdateCompilationAsync_ShouldKeepOldKey_WhenItPointsToAnotherAlbum()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity>
            {
                new() { Id = 8, Name = "Mix", IsCompilation = true },
                new() { Id = 9, Name = "Mix", IsCompilation = true }
            });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        await import.UpdateCompilationAsync(import.GetById(9)!, false, 30, Rok.Domain.Enums.ECompilationSource.Derived);

        // Assert
        Assert.Equal(8, import.GetFromCache("Mix", true, null)!.Id);
        Assert.Equal(9, import.GetFromCache("Mix", false, 30)!.Id);
    }

    [Fact(DisplayName = "homonymous_compilations_in_two_folders_stay_two_albums")]
    public async Task GetFromCache_ShouldKeepHomonymousCompilationsApart_WhenFoldersDiffer()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity>
            {
                new() { Id = 1, Name = "Hits", IsCompilation = true, AlbumPath = @"C:\m\Hits 1" },
                new() { Id = 2, Name = "Hits", IsCompilation = true, AlbumPath = @"C:\m\Hits 2" }
            });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? first = import.GetFromCache("Hits", true, null, @"C:\m\Hits 1");
        AlbumCacheItem? second = import.GetFromCache("Hits", true, null, @"C:\m\Hits 2");
        AlbumCacheItem? other = import.GetFromCache("Hits", true, null, @"C:\m\Hits 3");

        // Assert
        Assert.Equal(1, first!.Id);
        Assert.Equal(2, second!.Id);
        Assert.Null(other);
    }

    [Fact(DisplayName = "compilation_split_on_cd1_cd2_stays_one_album")]
    public async Task GetFromCache_ShouldMergeDiscSubfolders_OfTheSameCompilation()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity> { new() { Id = 1, Name = "Hits", IsCompilation = true, AlbumPath = @"C:\m\Hits\CD1" } });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? hit = import.GetFromCache("Hits", true, null, @"C:\m\Hits\CD2");

        // Assert
        Assert.Equal(1, hit!.Id);
    }

    [Fact(DisplayName = "non_compilation_key_unchanged")]
    public async Task GetFromCache_ShouldIgnoreFolder_WhenNotACompilation()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(new List<AlbumEntity>
            {
                new() { Id = 1, Name = "Album", ArtistId = 10, AlbumPath = @"C:\m\a" },
                new() { Id = 2, Name = "Orphan", AlbumPath = @"C:\m\b" }
            });
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.LoadCacheAsync();

        // Act
        AlbumCacheItem? withArtist = import.GetFromCache("Album", false, 10, @"C:\m\other");
        AlbumCacheItem? withoutArtist = import.GetFromCache("Orphan", false, null, @"C:\m\elsewhere");

        // Assert
        Assert.Equal(1, withArtist!.Id);
        Assert.Equal(2, withoutArtist!.Id);
    }

    [Fact(DisplayName = "NewlyCreatedIds should be empty on initialization")]
    public void NewlyCreatedIds_ShouldBeEmpty_OnInitialization()
    {
        // Arrange & Act
        AlbumImport import = new(Mock.Of<IAlbumRepository>(), TimeProvider.System);

        // Assert
        Assert.Empty(import.NewlyCreatedIds);
    }

    [Fact(DisplayName = "CreateAsync should add the new album id to NewlyCreatedIds")]
    public async Task CreateAsync_ShouldAddIdToNewlyCreatedIds()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(99L);
        AlbumImport import = new(repository.Object, TimeProvider.System);

        // Act
        await import.CreateAsync(new TrackFile { Album = "Black", Artist = "AC/DC", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Assert
        Assert.Single(import.NewlyCreatedIds);
        Assert.Equal(99L, import.NewlyCreatedIds[0]);
    }

    [Fact(DisplayName = "CreateAsync should not add more than 100 ids to NewlyCreatedIds")]
    public async Task CreateAsync_ShouldNotAddMoreThan100Ids_ToNewlyCreatedIds()
    {
        // Arrange
        long id = 1;
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(() => id++);
        AlbumImport import = new(repository.Object, TimeProvider.System);

        // Act
        for (int i = 0; i < 101; i++)
            await import.CreateAsync(new TrackFile { Album = $"Album{i}", Artist = "Artist", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Assert
        Assert.Equal(100, import.NewlyCreatedIds.Count);
    }

    [Fact(DisplayName = "LoadCacheAsync should clear NewlyCreatedIds")]
    public async Task LoadCacheAsync_ShouldClearNewlyCreatedIds()
    {
        // Arrange
        Mock<IAlbumRepository> repository = new();
        repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1L);
        repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<AlbumEntity>());
        AlbumImport import = new(repository.Object, TimeProvider.System);
        await import.CreateAsync(new TrackFile { Album = "Black", Artist = "AC/DC", FullPath = @"C:\m\t.mp3" }, 1, null);

        // Act
        await import.LoadCacheAsync();

        // Assert
        Assert.Empty(import.NewlyCreatedIds);
    }
}