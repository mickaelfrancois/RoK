# Import API Enrichment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After a library import, automatically call `MusicDataApiService` for the first 100 newly created artists and 100 newly created albums to pre-fetch images and metadata, without impacting import performance.

**Architecture:** A new `PostImportApiEnrichmentTask` (matching the existing `PostImportDominantColorTask` pattern) runs after the main import. `ArtistImport` and `AlbumImport` collect the IDs of newly created entities (capped at 100 each) and expose them for the task to consume. `ArtistApiService` and `AlbumApiService` are extracted behind interfaces to stay mockable in tests.

**Tech Stack:** .NET 10 / C# 13, xUnit + Moq, MiF.Mediator, MiF.Result, `IArtistApiService` / `IAlbumApiService` (new interfaces).

---

## File Map

| Action | File |
|---|---|
| Create | `src/Rok.Application/Features/Artists/Services/IArtistApiService.cs` |
| Create | `src/Rok.Application/Features/Albums/Services/IAlbumApiService.cs` |
| Modify | `src/Rok.Application/Features/Artists/Services/ArtistApiService.cs` |
| Modify | `src/Rok.Application/Features/Albums/Services/AlbumApiService.cs` |
| Modify | `src/Rok.Application/DependencyInjection.cs` |
| Modify | `src/Rok.Import/ArtistImport.cs` |
| Modify | `src/Rok.Import/AlbumImport.cs` |
| Create | `src/Rok.Import/Services/PostImportApiEnrichmentTask.cs` |
| Modify | `src/Rok.Import/ImportService.cs` |
| Modify | `src/Rok.Import/DependencyInjection.cs` |
| Modify | `tests/UnitTests/Rok.ImportTests/ArtistImportTests.cs` |
| Modify | `tests/UnitTests/Rok.ImportTests/AlbumImportTests.cs` |
| Create | `tests/UnitTests/Rok.ImportTests/Services/PostImportApiEnrichmentTaskTests.cs` |

---

## Task 1 — Extract `IArtistApiService` and `IAlbumApiService`

**Files:**
- Create: `src/Rok.Application/Features/Artists/Services/IArtistApiService.cs`
- Create: `src/Rok.Application/Features/Albums/Services/IAlbumApiService.cs`
- Modify: `src/Rok.Application/Features/Artists/Services/ArtistApiService.cs`
- Modify: `src/Rok.Application/Features/Albums/Services/AlbumApiService.cs`
- Modify: `src/Rok.Application/DependencyInjection.cs`

No TDD for interface extraction — it's a pure refactor. Build verification is the safety net.

- [ ] **Step 1: Create `IArtistApiService`**

```csharp
// src/Rok.Application/Features/Artists/Services/IArtistApiService.cs
using Rok.Application.Dto;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Application.Features.Artists.Services;

public interface IArtistApiService
{
    Task<ArtistApiUpdateResult> GetAndUpdateArtistDataAsync(ArtistDto artist, IArtistPictureService pictureService, IBackdropPicture backdropPicture);
}
```

- [ ] **Step 2: Create `IAlbumApiService`**

```csharp
// src/Rok.Application/Features/Albums/Services/IAlbumApiService.cs
using Rok.Application.Dto;
using Rok.Application.Interfaces.Pictures;

namespace Rok.Application.Features.Albums.Services;

public interface IAlbumApiService
{
    Task<AlbumApiUpdateResult> GetAndUpdateAlbumDataAsync(AlbumDto album, IAlbumPictureService pictureService);
}
```

- [ ] **Step 3: Implement `IArtistApiService` on `ArtistApiService`**

Change the class declaration from:
```csharp
public class ArtistApiService(...)
```
to:
```csharp
public class ArtistApiService(...) : IArtistApiService
```

- [ ] **Step 4: Implement `IAlbumApiService` on `AlbumApiService`**

Change:
```csharp
public class AlbumApiService(...)
```
to:
```csharp
public class AlbumApiService(...) : IAlbumApiService
```

- [ ] **Step 5: Update DI registrations in `Rok.Application/DependencyInjection.cs`**

Replace:
```csharp
services.AddTransient<AlbumApiService>();
services.AddTransient<ArtistApiService>();
```
with:
```csharp
services.AddTransient<IArtistApiService, ArtistApiService>();
services.AddTransient<IAlbumApiService, AlbumApiService>();
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build /p:Platform=x64
```

Expected: build succeeds with zero warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Application/Features/Artists/Services/IArtistApiService.cs \
        src/Rok.Application/Features/Albums/Services/IAlbumApiService.cs \
        src/Rok.Application/Features/Artists/Services/ArtistApiService.cs \
        src/Rok.Application/Features/Albums/Services/AlbumApiService.cs \
        src/Rok.Application/DependencyInjection.cs
git commit -m "refactor: extract IArtistApiService and IAlbumApiService interfaces"
```

---

## Task 2 — Extend `ArtistImport` with ID collection

**Files:**
- Modify: `src/Rok.Import/ArtistImport.cs`
- Modify: `tests/UnitTests/Rok.ImportTests/ArtistImportTests.cs`

- [ ] **Step 1: Write failing tests**

Append to `ArtistImportTests.cs`:

```csharp
[Fact(DisplayName = "NewlyCreatedIds should be empty on initialization")]
public void NewlyCreatedIds_ShouldBeEmpty_OnInitialization()
{
    // Arrange & Act
    ArtistImport import = new(Mock.Of<IArtistRepository>());

    // Assert
    Assert.Empty(import.NewlyCreatedIds);
}

[Fact(DisplayName = "CreateAsync should add the new artist id to NewlyCreatedIds")]
public async Task CreateAsync_ShouldAddIdToNewlyCreatedIds()
{
    // Arrange
    Mock<IArtistRepository> repository = new();
    repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(42L);
    ArtistImport import = new(repository.Object);

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
    ArtistImport import = new(repository.Object);

    // Act
    for (int i = 0; i < 101; i++)
        await import.CreateAsync(new TrackFile { Artist = $"Artist{i}", FullPath = @"C:\m\t.mp3" }, null);

    // Assert
    Assert.Equal(100, import.NewlyCreatedIds.Count);
}

[Fact(DisplayName = "LoadCacheAsync should clear NewlyCreatedIds")]
public async Task LoadCacheAsync_ShouldClearNewlyCreatedIds()
{
    // Arrange
    Mock<IArtistRepository> repository = new();
    repository.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(1L);
    repository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(Array.Empty<ArtistEntity>());
    ArtistImport import = new(repository.Object);
    await import.CreateAsync(new TrackFile { Artist = "Metallica", FullPath = @"C:\m\t.mp3" }, null);

    // Act
    await import.LoadCacheAsync();

    // Assert
    Assert.Empty(import.NewlyCreatedIds);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ArtistImportTests"
```

Expected: compilation error — `NewlyCreatedIds` does not exist.

- [ ] **Step 3: Implement the changes in `ArtistImport.cs`**

Add after the existing field declarations (after `_cache`):

```csharp
private const int MaxArtistsApiEnrichment = 100;

private readonly List<long> _newlyCreatedIds = new();

public IReadOnlyList<long> NewlyCreatedIds => _newlyCreatedIds;
```

In `LoadCacheAsync()`, add `_newlyCreatedIds.Clear();` right after `_cache.Clear();`:

```csharp
public async Task LoadCacheAsync()
{
    _cache.Clear();
    _newlyCreatedIds.Clear();

    IEnumerable<ArtistEntity> artists = await _artistRepository.GetAllAsync(RepositoryConnectionKind.Background);
    // ... rest unchanged
```

In `CreateAsync()`, add after `CreatedCount++;`:

```csharp
        CreatedCount++;

        if (_newlyCreatedIds.Count < MaxArtistsApiEnrichment)
            _newlyCreatedIds.Add(id);

        return cacheItem;
```

- [ ] **Step 4: Run tests and verify they pass**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~ArtistImportTests"
```

Expected: all `ArtistImportTests` pass.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Import/ArtistImport.cs tests/UnitTests/Rok.ImportTests/ArtistImportTests.cs
git commit -m "feat(import): collect newly created artist ids for API enrichment"
```

---

## Task 3 — Extend `AlbumImport` with ID collection

**Files:**
- Modify: `src/Rok.Import/AlbumImport.cs`
- Modify: `tests/UnitTests/Rok.ImportTests/AlbumImportTests.cs`

- [ ] **Step 1: Write failing tests**

Append to `AlbumImportTests.cs`:

```csharp
[Fact(DisplayName = "NewlyCreatedIds should be empty on initialization")]
public void NewlyCreatedIds_ShouldBeEmpty_OnInitialization()
{
    // Arrange & Act
    AlbumImport import = new(Mock.Of<IAlbumRepository>());

    // Assert
    Assert.Empty(import.NewlyCreatedIds);
}

[Fact(DisplayName = "CreateAsync should add the new album id to NewlyCreatedIds")]
public async Task CreateAsync_ShouldAddIdToNewlyCreatedIds()
{
    // Arrange
    Mock<IAlbumRepository> repository = new();
    repository.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(99L);
    AlbumImport import = new(repository.Object);

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
    AlbumImport import = new(repository.Object);

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
    AlbumImport import = new(repository.Object);
    await import.CreateAsync(new TrackFile { Album = "Black", Artist = "AC/DC", FullPath = @"C:\m\t.mp3" }, 1, null);

    // Act
    await import.LoadCacheAsync();

    // Assert
    Assert.Empty(import.NewlyCreatedIds);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~AlbumImportTests"
```

Expected: compilation error — `NewlyCreatedIds` does not exist.

- [ ] **Step 3: Implement the changes in `AlbumImport.cs`**

Add after the existing field declarations:

```csharp
private const int MaxAlbumsApiEnrichment = 100;

private readonly List<long> _newlyCreatedIds = new();

public IReadOnlyList<long> NewlyCreatedIds => _newlyCreatedIds;
```

In `LoadCacheAsync()`, add `_newlyCreatedIds.Clear();` right after `_cache.Clear();`:

```csharp
public async Task LoadCacheAsync()
{
    _cache.Clear();
    _newlyCreatedIds.Clear();

    IEnumerable<AlbumEntity> albums = await _albumRepository.GetAllAsync(RepositoryConnectionKind.Background);
    // ... rest unchanged
```

In `CreateAsync()`, add after `CreatedCount++;`:

```csharp
        CreatedCount++;

        if (_newlyCreatedIds.Count < MaxAlbumsApiEnrichment)
            _newlyCreatedIds.Add(id);

        return cacheItem;
```

- [ ] **Step 4: Run tests and verify they pass**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~AlbumImportTests"
```

Expected: all `AlbumImportTests` pass.

- [ ] **Step 5: Commit**

```bash
git add src/Rok.Import/AlbumImport.cs tests/UnitTests/Rok.ImportTests/AlbumImportTests.cs
git commit -m "feat(import): collect newly created album ids for API enrichment"
```

---

## Task 4 — Create `PostImportApiEnrichmentTask`

**Files:**
- Create: `src/Rok.Import/Services/PostImportApiEnrichmentTask.cs`
- Create: `tests/UnitTests/Rok.ImportTests/Services/PostImportApiEnrichmentTaskTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/UnitTests/Rok.ImportTests/Services/PostImportApiEnrichmentTaskTests.cs`:

```csharp
using MiF.Mediator;
using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class PostImportApiEnrichmentTaskTests
{
    private static async Task<ArtistImport> BuildArtistImport(int idCount)
    {
        long id = 1;
        Mock<IArtistRepository> repo = new();
        repo.Setup(r => r.AddAsync(It.IsAny<ArtistEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(() => id++);
        ArtistImport artistImport = new(repo.Object);

        for (int i = 0; i < idCount; i++)
            await artistImport.CreateAsync(new TrackFile { Artist = $"Artist{i}", FullPath = @"C:\m\t.mp3" }, null);

        return artistImport;
    }

    private static async Task<AlbumImport> BuildAlbumImport(int idCount)
    {
        long id = 1;
        Mock<IAlbumRepository> repo = new();
        repo.Setup(r => r.AddAsync(It.IsAny<AlbumEntity>(), It.IsAny<RepositoryConnectionKind>()))
            .ReturnsAsync(() => id++);
        AlbumImport albumImport = new(repo.Object);

        for (int i = 0; i < idCount; i++)
            await albumImport.CreateAsync(new TrackFile { Album = $"Album{i}", Artist = "Artist", FullPath = @"C:\m\t.mp3" }, 1, null);

        return albumImport;
    }

    private static PostImportApiEnrichmentTask BuildTask(
        ArtistImport artistImport,
        AlbumImport albumImport,
        Mock<IArtistApiService> artistApi,
        Mock<IAlbumApiService> albumApi,
        Mock<IMediator> mediator)
    {
        return new PostImportApiEnrichmentTask(
            artistImport,
            albumImport,
            artistApi.Object,
            albumApi.Object,
            Mock.Of<IArtistPictureService>(),
            Mock.Of<IAlbumPictureService>(),
            Mock.Of<IBackdropPicture>(),
            mediator.Object,
            NullLogger<PostImportApiEnrichmentTask>.Instance);
    }

    [Fact(DisplayName = "RunAsync should not call any api service when no newly created ids exist")]
    public async Task RunAsync_ShouldNotCallAnyApiService_WhenNoNewlyCreatedIdsExist()
    {
        // Arrange
        ArtistImport artistImport = new(Mock.Of<IArtistRepository>());
        AlbumImport albumImport = new(Mock.Of<IAlbumRepository>());
        Mock<IArtistApiService> artistApi = new();
        Mock<IAlbumApiService> albumApi = new();
        Mock<IMediator> mediator = new();
        PostImportApiEnrichmentTask task = BuildTask(artistImport, albumImport, artistApi, albumApi, mediator);

        // Act
        await task.RunAsync(CancellationToken.None);

        // Assert
        artistApi.Verify(s => s.GetAndUpdateArtistDataAsync(It.IsAny<ArtistDto>(), It.IsAny<IArtistPictureService>(), It.IsAny<IBackdropPicture>()), Times.Never);
        albumApi.Verify(s => s.GetAndUpdateAlbumDataAsync(It.IsAny<AlbumDto>(), It.IsAny<IAlbumPictureService>()), Times.Never);
    }

    [Fact(DisplayName = "EnrichArtistsAsync should call the api service for each newly created artist id")]
    public async Task EnrichArtistsAsync_ShouldCallApiService_ForEachNewlyCreatedArtistId()
    {
        // Arrange
        ArtistImport artistImport = await BuildArtistImport(2);
        AlbumImport albumImport = new(Mock.Of<IAlbumRepository>());
        Mock<IArtistApiService> artistApi = new();
        artistApi.Setup(s => s.GetAndUpdateArtistDataAsync(It.IsAny<ArtistDto>(), It.IsAny<IArtistPictureService>(), It.IsAny<IBackdropPicture>()))
            .ReturnsAsync(ArtistApiUpdateResult.None);
        Mock<IMediator> mediator = new();
        mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetArtistByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArtistDto>.Success(new ArtistDto { Id = 1, Name = "Artist" }));
        PostImportApiEnrichmentTask task = BuildTask(artistImport, albumImport, artistApi, new(), mediator);

        // Act
        await task.EnrichArtistsAsync(CancellationToken.None);

        // Assert
        artistApi.Verify(s => s.GetAndUpdateArtistDataAsync(It.IsAny<ArtistDto>(), It.IsAny<IArtistPictureService>(), It.IsAny<IBackdropPicture>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "EnrichAlbumsAsync should call the api service for each newly created album id")]
    public async Task EnrichAlbumsAsync_ShouldCallApiService_ForEachNewlyCreatedAlbumId()
    {
        // Arrange
        ArtistImport artistImport = new(Mock.Of<IArtistRepository>());
        AlbumImport albumImport = await BuildAlbumImport(3);
        Mock<IAlbumApiService> albumApi = new();
        albumApi.Setup(s => s.GetAndUpdateAlbumDataAsync(It.IsAny<AlbumDto>(), It.IsAny<IAlbumPictureService>()))
            .ReturnsAsync(AlbumApiUpdateResult.None);
        Mock<IMediator> mediator = new();
        mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAlbumByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AlbumDto>.Success(new AlbumDto { Id = 1, Name = "Album" }));
        PostImportApiEnrichmentTask task = BuildTask(artistImport, albumImport, new(), albumApi, mediator);

        // Act
        await task.EnrichAlbumsAsync(CancellationToken.None);

        // Assert
        albumApi.Verify(s => s.GetAndUpdateAlbumDataAsync(It.IsAny<AlbumDto>(), It.IsAny<IAlbumPictureService>()), Times.Exactly(3));
    }

    [Fact(DisplayName = "EnrichArtistsAsync should continue processing remaining artists when one throws")]
    public async Task EnrichArtistsAsync_ShouldContinue_WhenOneArtistThrows()
    {
        // Arrange
        ArtistImport artistImport = await BuildArtistImport(2);
        AlbumImport albumImport = new(Mock.Of<IAlbumRepository>());
        Mock<IArtistApiService> artistApi = new();
        artistApi.SetupSequence(s => s.GetAndUpdateArtistDataAsync(It.IsAny<ArtistDto>(), It.IsAny<IArtistPictureService>(), It.IsAny<IBackdropPicture>()))
            .Returns(Task.FromException<ArtistApiUpdateResult>(new InvalidOperationException("API failure")))
            .ReturnsAsync(ArtistApiUpdateResult.None);
        Mock<IMediator> mediator = new();
        mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetArtistByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArtistDto>.Success(new ArtistDto { Id = 1, Name = "Artist" }));
        PostImportApiEnrichmentTask task = BuildTask(artistImport, albumImport, artistApi, new(), mediator);

        // Act — must not throw
        await task.EnrichArtistsAsync(CancellationToken.None);

        // Assert — second artist was still processed
        artistApi.Verify(s => s.GetAndUpdateArtistDataAsync(It.IsAny<ArtistDto>(), It.IsAny<IArtistPictureService>(), It.IsAny<IBackdropPicture>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "EnrichArtistsAsync should stop processing when cancellation is requested")]
    public async Task EnrichArtistsAsync_ShouldStop_WhenCancellationIsRequested()
    {
        // Arrange
        ArtistImport artistImport = await BuildArtistImport(2);
        AlbumImport albumImport = new(Mock.Of<IAlbumRepository>());
        Mock<IArtistApiService> artistApi = new();
        Mock<IMediator> mediator = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        PostImportApiEnrichmentTask task = BuildTask(artistImport, albumImport, artistApi, new(), mediator);

        // Act
        await task.EnrichArtistsAsync(cts.Token);

        // Assert
        mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetArtistByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PostImportApiEnrichmentTaskTests"
```

Expected: compilation error — `PostImportApiEnrichmentTask` does not exist.

- [ ] **Step 3: Implement `PostImportApiEnrichmentTask`**

Create `src/Rok.Import/Services/PostImportApiEnrichmentTask.cs`:

```csharp
using Microsoft.Extensions.Logging;
using MiF.Mediator;
using MiF.Result;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Pictures;
using Rok.Shared;

namespace Rok.Import.Services;

public class PostImportApiEnrichmentTask(
    ArtistImport artistImport,
    AlbumImport albumImport,
    IArtistApiService artistApiService,
    IAlbumApiService albumApiService,
    IArtistPictureService artistPictureService,
    IAlbumPictureService albumPictureService,
    IBackdropPicture backdropPicture,
    IMediator mediator,
    ILogger<PostImportApiEnrichmentTask> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using PerfLogger perf = new PerfLogger(logger).Parameters("Post import API enrichment task");
        await EnrichArtistsAsync(cancellationToken);
        await EnrichAlbumsAsync(cancellationToken);
    }

    public async Task EnrichArtistsAsync(CancellationToken cancellationToken)
    {
        int enriched = 0;

        foreach (long artistId in artistImport.NewlyCreatedIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                Result<ArtistDto> result = await mediator.SendMessageAsync(new GetArtistByIdQuery(artistId), cancellationToken);

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Artist {Id} not found for API enrichment.", artistId);
                    continue;
                }

                await artistApiService.GetAndUpdateArtistDataAsync(result.Value, artistPictureService, backdropPicture);
                enriched++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enrich artist {Id}.", artistId);
            }
        }

        logger.LogInformation("API enrichment: {Count} artists enriched.", enriched);
    }

    public async Task EnrichAlbumsAsync(CancellationToken cancellationToken)
    {
        int enriched = 0;

        foreach (long albumId in albumImport.NewlyCreatedIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                Result<AlbumDto> result = await mediator.SendMessageAsync(new GetAlbumByIdQuery(albumId), cancellationToken);

                if (!result.IsSuccess)
                {
                    logger.LogWarning("Album {Id} not found for API enrichment.", albumId);
                    continue;
                }

                await albumApiService.GetAndUpdateAlbumDataAsync(result.Value, albumPictureService);
                enriched++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to enrich album {Id}.", albumId);
            }
        }

        logger.LogInformation("API enrichment: {Count} albums enriched.", enriched);
    }
}
```

- [ ] **Step 4: Run tests and verify they pass**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64 --filter "FullyQualifiedName~PostImportApiEnrichmentTaskTests"
```

Expected: all `PostImportApiEnrichmentTaskTests` pass.

- [ ] **Step 5: Run the full Import test suite to check for regressions**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Rok.Import/Services/PostImportApiEnrichmentTask.cs \
        tests/UnitTests/Rok.ImportTests/Services/PostImportApiEnrichmentTaskTests.cs
git commit -m "feat(import): add PostImportApiEnrichmentTask for artist and album enrichment"
```

---

## Task 5 — Wire into `ImportService` and register in DI

**Files:**
- Modify: `src/Rok.Import/ImportService.cs`
- Modify: `src/Rok.Import/DependencyInjection.cs`

No new tests — the wiring is structural. The full suite build is the verification.

- [ ] **Step 1: Add `PostImportApiEnrichmentTask` as a constructor parameter in `ImportService`**

In `ImportService.cs`, add `PostImportApiEnrichmentTask postImportApiEnrichmentTask` to the primary constructor and store it as a field, following the exact pattern used for `postImportDominantColorTask`:

Constructor (add after `postImportDominantColorTask`):
```csharp
public class ImportService(
    // ... existing params ...
    PostImportDominantColorTask postImportDominantColorTask,
    PostImportApiEnrichmentTask postImportApiEnrichmentTask,   // ADD THIS
    ImportMessageThrottler messageThrottler,
    ILogger<ImportService> logger) : IImport
```

Field (add after the existing `_postImportDominantColorTask` field):
```csharp
private readonly PostImportApiEnrichmentTask _postImportApiEnrichmentTask = Guard.Against.Null(postImportApiEnrichmentTask);
```

- [ ] **Step 2: Call `RunAsync` in `ImportAsync`**

In `ImportService.ImportAsync()`, add the enrichment task call immediately after the dominant color task call:

```csharp
        if (!errorOccurred)
            await _postImportDominantColorTask.RunAsync(cancellationToken);

        if (!errorOccurred)                                                   // ADD THIS
            await _postImportApiEnrichmentTask.RunAsync(cancellationToken);   // ADD THIS
```

- [ ] **Step 3: Register in DI**

In `src/Rok.Import/DependencyInjection.cs`, add after `PostImportDominantColorTask`:

```csharp
        services.AddSingleton<PostImportDominantColorTask>();
        services.AddTransient<PostImportApiEnrichmentTask>();   // ADD THIS
```

Note: `PostImportApiEnrichmentTask` is `AddTransient` (not `AddSingleton`) because it injects `IArtistApiService` and `IAlbumApiService` which are registered as `Transient` in `Rok.Application`. Injecting a Transient into a Singleton is a captive dependency anti-pattern.

Since `ImportService` is `AddScoped`, it safely resolves a `Transient` at request time.

- [ ] **Step 4: Build**

```bash
dotnet build /p:Platform=x64
```

Expected: zero warnings, zero errors.

- [ ] **Step 5: Run all import tests**

```bash
dotnet test tests/UnitTests/Rok.ImportTests/Rok.ImportTests.csproj /p:Platform=x64
```

Expected: all tests pass.

- [ ] **Step 6: Run full test suite**

```bash
dotnet test /p:Platform=x64
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Rok.Import/ImportService.cs src/Rok.Import/DependencyInjection.cs
git commit -m "feat(import): wire PostImportApiEnrichmentTask into import pipeline"
```
