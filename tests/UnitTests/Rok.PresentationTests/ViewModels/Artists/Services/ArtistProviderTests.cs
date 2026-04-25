using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Artists.Query;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Artists.Interfaces;
using Rok.ViewModels.Artists.Services;

namespace Rok.PresentationTests.ViewModels.Artists.Services;

public class ArtistProviderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IArtistViewModelFactory> _vmFactory = new();
    private readonly Mock<IResourceService> _resource = new();

    private ArtistProvider BuildService()
    {
        ArtistsDataLoader loader = new(_mediator.Object, _vmFactory.Object, NullLogger<ArtistsDataLoader>.Instance);
        ArtistsFilter filter = new(_resource.Object);
        ArtistsGroupCategory grouper = new(_resource.Object);
        return new ArtistProvider(loader, filter, grouper);
    }

    [Fact(DisplayName = "LoadAsync should call mediator for both genres and artists")]
    public async Task LoadAsync_ShouldCallMediatorForGenresAndArtists()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GenreDto>());
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllArtistsQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<ArtistDto>());
        ArtistProvider sut = BuildService();

        // Act
        await sut.LoadAsync(excludeArtistsWithoutAlbum: false);

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllArtistsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetProcessedData should return empty result when no artists are loaded")]
    public void GetProcessedData_ShouldReturnEmptyResult_WhenNoArtists()
    {
        // Arrange
        ArtistProvider sut = BuildService();

        // Act
        ArtistProviderResult result = sut.GetProcessedData(GroupingConstants.None, new(), new(), new());

        // Assert
        Assert.Empty(result.FilteredItems);
    }

    [Fact(DisplayName = "Clear should reset ViewModels and Genres to empty")]
    public void Clear_ShouldResetCollections()
    {
        // Arrange
        ArtistProvider sut = BuildService();

        // Act
        sut.Clear();

        // Assert
        Assert.Empty(sut.ViewModels);
        Assert.Empty(sut.Genres);
    }

    [Fact(DisplayName = "SetArtists should accept an empty list without invoking the factory")]
    public void SetArtists_WithEmptyList_ShouldNotInvokeFactory()
    {
        // Arrange
        ArtistProvider sut = BuildService();

        // Act
        sut.SetArtists(new List<ArtistDto>());

        // Assert
        Assert.Empty(sut.ViewModels);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "GetFilterLabel should request the matching resource key")]
    public void GetFilterLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("artistsViewFilterByNeverListened")).Returns("Never listened");
        ArtistProvider sut = BuildService();

        // Act
        string label = sut.GetFilterLabel(ArtistsFilter.KFilterByNeverListened);

        // Assert
        Assert.Equal("Never listened", label);
    }

    [Fact(DisplayName = "GetGroupByLabel should request the matching resource key")]
    public void GetGroupByLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("artistsViewGroupByYear")).Returns("Year");
        ArtistProvider sut = BuildService();

        // Act
        string label = sut.GetGroupByLabel(GroupingConstants.Year);

        // Assert
        Assert.Equal("Year", label);
    }
}
