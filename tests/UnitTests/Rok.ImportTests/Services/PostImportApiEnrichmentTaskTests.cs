using MiF.Mediator.Interfaces;
using MiF.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Features.Albums.Query;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;
using Rok.Import.Models;
using Rok.Import.Services;

namespace Rok.ImportTests.Services;

public class PostImportApiEnrichmentTaskTests
{
    private static async Task<ArtistImport> BuildArtistImportAsync(int idCount)
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

    private static async Task<AlbumImport> BuildAlbumImportAsync(int idCount)
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
        ArtistImport artistImport = await BuildArtistImportAsync(2);
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
        AlbumImport albumImport = await BuildAlbumImportAsync(3);
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
        ArtistImport artistImport = await BuildArtistImportAsync(2);
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
        ArtistImport artistImport = await BuildArtistImportAsync(2);
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
