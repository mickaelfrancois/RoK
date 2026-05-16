using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Albums.Requests;
using Rok.Application.Features.Genres.Requests;
using Rok.Application.Features.Tags.Requests;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Albums.Interfaces;
using Rok.ViewModels.Albums.Services;

namespace Rok.PresentationTests.ViewModels.Albums.Services;

public class AlbumProviderTests
{
    private readonly FakeMediator _mediator = new();
    private readonly Mock<IAlbumViewModelFactory> _vmFactory = new();
    private readonly Mock<IResourceService> _resource = new();

    private AlbumProvider BuildService()
    {
        AlbumsDataLoader loader = new(_mediator, _vmFactory.Object, NullLogger<AlbumsDataLoader>.Instance);
        AlbumsFilter filter = new(_resource.Object);
        AlbumsGroupCategory grouper = new(_resource.Object);
        return new AlbumProvider(loader, filter, grouper);
    }

    [Fact(DisplayName = "LoadAsync should call mediator for both genres and albums")]
    public async Task LoadAsync_ShouldCallMediatorForGenresAndAlbums()
    {
        // Arrange
        _mediator.Setup<GetAllGenresRequest, IEnumerable<GenreDto>>()
                 .Returns(new List<GenreDto>());
        _mediator.Setup<GetAllAlbumsRequest, IEnumerable<AlbumDto>>()
                 .Returns(new List<AlbumDto>());
        AlbumProvider sut = BuildService();

        // Act
        await sut.LoadAsync();

        // Assert
        Assert.Single(_mediator.Sent<GetAllGenresRequest>());
        Assert.Single(_mediator.Sent<GetAllAlbumsRequest>());
    }

    [Fact(DisplayName = "GetProcessedData should return empty result when no albums are loaded")]
    public void GetProcessedData_ShouldReturnEmptyResult_WhenNoAlbums()
    {
        // Arrange
        AlbumProvider sut = BuildService();

        // Act
        AlbumProviderResult result = sut.GetProcessedData(GroupingConstants.None, new(), new(), new());

        // Assert
        Assert.Empty(result.FilteredItems);
    }

    [Fact(DisplayName = "Clear should reset ViewModels and Genres to empty")]
    public void Clear_ShouldResetCollections()
    {
        // Arrange
        AlbumProvider sut = BuildService();

        // Act
        sut.Clear();

        // Assert
        Assert.Empty(sut.ViewModels);
        Assert.Empty(sut.Genres);
    }

    [Fact(DisplayName = "SetAlbums should accept an empty album list without invoking the factory")]
    public void SetAlbums_WithEmptyList_ShouldNotInvokeFactory()
    {
        // Arrange
        AlbumProvider sut = BuildService();

        // Act
        sut.SetAlbums(new List<AlbumDto>());

        // Assert
        Assert.Empty(sut.ViewModels);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "GetFilterLabel should request the matching resource key")]
    public void GetFilterLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("albumsViewFilterByLive")).Returns("Live");
        AlbumProvider sut = BuildService();

        // Act
        string label = sut.GetFilterLabel(AlbumsFilter.KFilterByLive);

        // Assert
        Assert.Equal("Live", label);
    }

    [Fact(DisplayName = "GetGroupByLabel should request the matching resource key")]
    public void GetGroupByLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("albumsViewGroupByYear")).Returns("Year");
        AlbumProvider sut = BuildService();

        // Act
        string label = sut.GetGroupByLabel(GroupingConstants.Year);

        // Assert
        Assert.Equal("Year", label);
    }
}