using MiF.Mediator.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Dto;
using Rok.Application.Features.Genres.Query;
using Rok.Application.Features.Tracks.Query;
using Rok.Application.Interfaces;
using Rok.Application.Services.Filters;
using Rok.Application.Services.Grouping;
using Rok.ViewModels.Tracks.Interfaces;
using Rok.ViewModels.Tracks.Services;

namespace Rok.PresentationTests.ViewModels.Tracks.Services;

public class TrackProviderTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ITrackViewModelFactory> _vmFactory = new();
    private readonly Mock<IResourceService> _resource = new();

    private TrackProvider BuildService()
    {
        TracksDataLoader loader = new(_mediator.Object, _vmFactory.Object, NullLogger<TracksDataLoader>.Instance);
        TracksFilter filter = new(_resource.Object);
        TracksGroupCategory grouper = new(_resource.Object);
        return new TrackProvider(loader, filter, grouper);
    }

    [Fact(DisplayName = "LoadAsync should call mediator for both genres and tracks")]
    public async Task LoadAsync_ShouldCallMediatorForGenresAndTracks()
    {
        // Arrange
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<GenreDto>());
        _mediator.Setup(m => m.SendMessageAsync(It.IsAny<GetAllTracksQuery>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<TrackDto>());
        TrackProvider sut = BuildService();

        // Act
        await sut.LoadAsync();

        // Assert
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllGenresQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.SendMessageAsync(It.IsAny<GetAllTracksQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "GetProcessedData should return empty result when no tracks are loaded")]
    public void GetProcessedData_ShouldReturnEmptyResult_WhenNoTracks()
    {
        // Arrange
        TrackProvider sut = BuildService();

        // Act
        TrackProviderResult result = sut.GetProcessedData(GroupingConstants.None, new(), new());

        // Assert
        Assert.Empty(result.FilteredItems);
    }

    [Fact(DisplayName = "Clear should reset ViewModels and Genres to empty")]
    public void Clear_ShouldResetCollections()
    {
        // Arrange
        TrackProvider sut = BuildService();

        // Act
        sut.Clear();

        // Assert
        Assert.Empty(sut.ViewModels);
        Assert.Empty(sut.Genres);
    }

    [Fact(DisplayName = "SetTracks should accept an empty list without invoking the factory")]
    public void SetTracks_WithEmptyList_ShouldNotInvokeFactory()
    {
        // Arrange
        TrackProvider sut = BuildService();

        // Act
        sut.SetTracks(new List<TrackDto>());

        // Assert
        Assert.Empty(sut.ViewModels);
        _vmFactory.Verify(f => f.Create(), Times.Never);
    }

    [Fact(DisplayName = "GetFilterLabel should request the matching resource key")]
    public void GetFilterLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("tracksViewFilterByLive")).Returns("Live");
        TrackProvider sut = BuildService();

        // Act
        string label = sut.GetFilterLabel(TracksFilter.KFilterByLive);

        // Assert
        Assert.Equal("Live", label);
    }

    [Fact(DisplayName = "GetGroupByLabel should request the matching resource key")]
    public void GetGroupByLabel_ShouldRequestResourceKey()
    {
        // Arrange
        _resource.Setup(r => r.GetString("tracksViewGroupByGenre")).Returns("Genre");
        TrackProvider sut = BuildService();

        // Act
        string label = sut.GetGroupByLabel(GroupingConstants.Genre);

        // Assert
        Assert.Equal("Genre", label);
    }
}
