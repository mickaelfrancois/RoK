using Moq;
using Rok.Application.Features.Statistics;
using Rok.Application.Features.Statistics.Requests;
using Rok.Application.Interfaces.Repositories;

namespace Rok.ApplicationTests.Features.Statistics.Requests;

public class GetStatisticsQueryHandlerTests
{
    [Fact(DisplayName = "Handle should aggregate totals and listened counts from repositories")]
    public async Task Handle_ShouldAggregateTotals_AndListenedCounts_FromRepositories()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, Size = 1000, Duration = 200, ListenCount = 5, MusicFile = "a.mp3" },
            new() { Id = 2, Size = 2000, Duration = 300, ListenCount = 0, MusicFile = "b.mp3" },
            new() { Id = 3, Size = 500, Duration = 100, ListenCount = 2, MusicFile = "c.flac" }
        };
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, IsLive = true, ListenCount = 3, Name = "Live" },
            new() { Id = 2, IsCompilation = true, ListenCount = 1, Name = "Comp" },
            new() { Id = 3, IsBestOf = true, ListenCount = 7, Name = "Best" },
            new() { Id = 4, ListenCount = 4, Name = "Studio" }
        };
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, Name = "Artist1", ListenCount = 10 },
            new() { Id = 2, Name = "Artist2", ListenCount = 2 }
        };
        List<GenreEntity> genres = new()
        {
            new() { Id = 1, Name = "Rock", ArtistCount = 5, ListenCount = 20 },
            new() { Id = 2, Name = "Pop", ArtistCount = 2, ListenCount = 8 }
        };

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IArtistRepository> artistRepository = new();
        artistRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        Mock<IGenreRepository> genreRepository = new();
        genreRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres);

        GetStatisticsRequestHandler handler = new(trackRepository.Object, albumRepository.Object, artistRepository.Object, genreRepository.Object);

        // Act
        UserStatisticsDto result = await handler.Handle(new GetStatisticsRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalTracks);
        Assert.Equal(3500, result.TotalSizeBytes);
        Assert.Equal(600, result.TotalDurationSeconds);
        Assert.Equal(4, result.TotalAlbums);
        Assert.Equal(2, result.TotalArtists);
        Assert.Equal(2, result.TotalGenres);
        Assert.Equal(2, result.TracksListenedCount);
        Assert.Equal(1, result.TracksNeverListenedCount);
    }

    [Fact(DisplayName = "Handle should group tracks by file extension and mark unknown when missing")]
    public async Task Handle_ShouldGroupTracksByFileExtension_AndMarkUnknownWhenMissing()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, MusicFile = "song.mp3" },
            new() { Id = 2, MusicFile = "other.MP3" },
            new() { Id = 3, MusicFile = "lossless.flac" },
            new() { Id = 4, MusicFile = "noext" }
        };

        GetStatisticsRequestHandler handler = BuildHandler(tracks, new(), new(), new());

        // Act
        UserStatisticsDto result = await handler.Handle(new GetStatisticsRequest(), CancellationToken.None);

        // Assert
        NamedCount mp3 = Assert.Single(result.TracksByFileType, c => c.Name == "mp3");
        Assert.Equal(2, mp3.Count);
        Assert.Contains(result.TracksByFileType, c => c.Name == "flac");
        Assert.Contains(result.TracksByFileType, c => c.Name == "unknown");
    }

    [Fact(DisplayName = "Handle should count studio albums as those without live compilation or best of flag")]
    public async Task Handle_ShouldCountStudioAlbums_AsThoseWithoutLiveCompilationOrBestOfFlag()
    {
        // Arrange
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, IsLive = true },
            new() { Id = 2, IsCompilation = true },
            new() { Id = 3, IsBestOf = true },
            new() { Id = 4 },
            new() { Id = 5 }
        };

        GetStatisticsRequestHandler handler = BuildHandler(new(), albums, new(), new());

        // Act
        UserStatisticsDto result = await handler.Handle(new GetStatisticsRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(2, result.AlbumsByType.First(c => c.Name == "studio").Count);
        Assert.Equal(1, result.AlbumsByType.First(c => c.Name == "live").Count);
        Assert.Equal(1, result.AlbumsByType.First(c => c.Name == "compilation").Count);
        Assert.Equal(1, result.AlbumsByType.First(c => c.Name == "bestof").Count);
    }

    [Fact(DisplayName = "Handle should expose top 20 items ordered by listen count")]
    public async Task Handle_ShouldExposeTop20Items_OrderedByListenCount()
    {
        // Arrange
        List<TrackEntity> tracks = Enumerable.Range(1, 25)
            .Select(i => new TrackEntity { Id = i, Title = $"T{i}", ListenCount = i })
            .ToList();

        GetStatisticsRequestHandler handler = BuildHandler(tracks, new(), new(), new());

        // Act
        UserStatisticsDto result = await handler.Handle(new GetStatisticsRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(20, result.TopTracks.Count);
        Assert.Equal(25, result.TopTracks[0].ListenCount);
        Assert.Equal(6, result.TopTracks[^1].ListenCount);
    }

    private static GetStatisticsRequestHandler BuildHandler(List<TrackEntity> tracks, List<AlbumEntity> albums, List<ArtistEntity> artists, List<GenreEntity> genres)
    {
        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks);
        Mock<IAlbumRepository> albumRepository = new();
        albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums);
        Mock<IArtistRepository> artistRepository = new();
        artistRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists);
        Mock<IGenreRepository> genreRepository = new();
        genreRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres);

        return new GetStatisticsRequestHandler(trackRepository.Object, albumRepository.Object, artistRepository.Object, genreRepository.Object);
    }
}
