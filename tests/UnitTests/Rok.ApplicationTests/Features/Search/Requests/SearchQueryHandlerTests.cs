using Moq;
using Rok.Application.Features.Search.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Interfaces.Entities;

namespace Rok.ApplicationTests.Features.Search.Requests;

public class SearchQueryHandlerTests
{
    [Fact(DisplayName = "Handle should aggregate results from album artist and track repositories")]
    public async Task Handle_ShouldAggregateResults_FromAlbumArtistAndTrackRepositories()
    {
        // Arrange
        List<IAlbumEntity> albums = new() { new AlbumEntity { Id = 1, Name = "Album" } };
        List<IArtistEntity> artists = new()
        {
            new ArtistEntity { Id = 2, Name = "Artist A" },
            new ArtistEntity { Id = 3, Name = "Artist B" }
        };
        List<TrackEntity> tracks = new() { new TrackEntity { Id = 4, Title = "Track" } };

        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.SearchAsync("queen", It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IArtistRepository> artistRepository = new();
        artistRepository.Setup(r => r.SearchAsync("queen", It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.SearchAsync("queen", It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);

        SearchRequestHandler handler = new(albumRepository.Object, artistRepository.Object, trackRepository.Object);

        // Act
        SearchDto result = await handler.Handle(new SearchRequest { Name = "queen" }, CancellationToken.None);

        // Assert
        Assert.Single(result.Albums);
        Assert.Equal(2, result.Artists.Count);
        Assert.Single(result.Tracks);
        Assert.Equal(4, result.ResultCount);
    }
}
