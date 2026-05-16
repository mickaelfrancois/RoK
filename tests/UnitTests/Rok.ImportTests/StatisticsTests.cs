using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;

namespace Rok.ImportTests;

public class StatisticsTests
{
    private static (Statistics service, Mock<ITrackRepository> track, Mock<IAlbumRepository> album, Mock<IArtistRepository> artist, Mock<IGenreRepository> genre) Build(
        IEnumerable<TrackEntity>? tracks = null,
        IEnumerable<AlbumEntity>? albums = null,
        IEnumerable<ArtistEntity>? artists = null,
        IEnumerable<GenreEntity>? genres = null)
    {
        Mock<ITrackRepository> trackRepo = new();
        trackRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks ?? Array.Empty<TrackEntity>());
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albums ?? Array.Empty<AlbumEntity>());
        Mock<IArtistRepository> artistRepo = new();
        artistRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artists ?? Array.Empty<ArtistEntity>());
        Mock<IGenreRepository> genreRepo = new();
        genreRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genres ?? Array.Empty<GenreEntity>());
        return (new Statistics(trackRepo.Object, albumRepo.Object, artistRepo.Object, genreRepo.Object, NullLogger<Statistics>.Instance), trackRepo, albumRepo, artistRepo, genreRepo);
    }

    [Fact(DisplayName = "UpdateAlbumsAsync should do nothing when the id list is empty")]
    public async Task UpdateAlbumsAsync_ShouldDoNothing_WhenIdListIsEmpty()
    {
        // Arrange
        (Statistics service, _, Mock<IAlbumRepository> album, _, _) = Build();

        // Act
        await service.UpdateAlbumsAsync(Array.Empty<long>());

        // Assert
        album.Verify(r => r.UpdateStatisticsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "UpdateAlbumsAsync should update album track count and duration from its tracks")]
    public async Task UpdateAlbumsAsync_ShouldUpdateAlbum_TrackCountAndDuration_FromItsTracks()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, AlbumId = 10, Duration = 120 },
            new() { Id = 2, AlbumId = 10, Duration = 180 },
            new() { Id = 3, AlbumId = 99, Duration = 999 }
        };
        List<AlbumEntity> albums = new() { new() { Id = 10, TrackCount = 0, Duration = 0 } };
        (Statistics service, _, Mock<IAlbumRepository> album, _, _) = Build(tracks, albums);

        // Act
        await service.UpdateAlbumsAsync(new[] { 10L });

        // Assert
        album.Verify(r => r.UpdateStatisticsAsync(10, 2, 300, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateAlbumsAsync should skip update when statistics already match the repository")]
    public async Task UpdateAlbumsAsync_ShouldSkipUpdate_WhenStatisticsAlreadyMatchRepository()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, AlbumId = 10, Duration = 200 }
        };
        List<AlbumEntity> albums = new() { new() { Id = 10, TrackCount = 1, Duration = 200 } };
        (Statistics service, _, Mock<IAlbumRepository> album, _, _) = Build(tracks, albums);

        // Act
        await service.UpdateAlbumsAsync(new[] { 10L });

        // Assert
        album.Verify(r => r.UpdateStatisticsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
    }

    [Fact(DisplayName = "UpdateArtistsAsync should compute album counts per type for the artist")]
    public async Task UpdateArtistsAsync_ShouldComputeAlbumCountsPerType_ForArtist()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, ArtistId = 1, Duration = 100 },
            new() { Id = 2, ArtistId = 1, Duration = 200 }
        };
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, ArtistId = 1, Year = 1990 },
            new() { Id = 2, ArtistId = 1, Year = 2000, IsLive = true },
            new() { Id = 3, ArtistId = 1, IsBestOf = true },
            new() { Id = 4, ArtistId = 1, IsCompilation = true }
        };
        List<ArtistEntity> artists = new() { new() { Id = 1 } };
        (Statistics service, _, _, Mock<IArtistRepository> artist, _) = Build(tracks, albums, artists);

        // Act
        await service.UpdateArtistsAsync(new[] { 1L });

        // Assert
        artist.Verify(r => r.UpdateStatisticsAsync(1, 2, 300, 1, 1, 1, 1, 1990, 1990, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateArtistsAsync should leave year bounds null when no qualifying album has a year")]
    public async Task UpdateArtistsAsync_ShouldLeaveYearBoundsNull_WhenNoQualifyingAlbumHasYear()
    {
        // Arrange
        List<AlbumEntity> albums = new() { new() { Id = 1, ArtistId = 1 } };
        List<ArtistEntity> artists = new() { new() { Id = 1 } };
        (Statistics service, _, _, Mock<IArtistRepository> artist, _) = Build(Array.Empty<TrackEntity>(), albums, artists);

        // Act
        await service.UpdateArtistsAsync(new[] { 1L });

        // Assert
        artist.Verify(r => r.UpdateStatisticsAsync(1, 0, 0, 1, 0, 0, 0, null, null, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateArtistsAsync should skip update when artist statistics already match")]
    public async Task UpdateArtistsAsync_ShouldSkipUpdate_WhenArtistStatisticsAlreadyMatch()
    {
        // Arrange
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, TrackCount = 0, TotalDurationSeconds = 0, AlbumCount = 0, BestofCount = 0, LiveCount = 0, CompilationCount = 0 }
        };
        (Statistics service, _, _, Mock<IArtistRepository> artist, _) = Build(Array.Empty<TrackEntity>(), Array.Empty<AlbumEntity>(), artists);

        // Act
        await service.UpdateArtistsAsync(new[] { 1L });

        // Assert
        artist.Verify(r => r.UpdateStatisticsAsync(
            It.IsAny<long>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<RepositoryConnectionKind>()),
            Times.Never);
    }

    [Fact(DisplayName = "UpdateGenresAsync should update genre statistics when counts differ")]
    public async Task UpdateGenresAsync_ShouldUpdateGenreStatistics_WhenCountsDiffer()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1, GenreId = 1, Duration = 100 },
            new() { Id = 2, GenreId = 1, Duration = 200 }
        };
        List<AlbumEntity> albums = new()
        {
            new() { Id = 1, GenreId = 1 },
            new() { Id = 2, GenreId = 1, IsLive = true }
        };
        List<ArtistEntity> artists = new()
        {
            new() { Id = 1, GenreId = 1 }
        };
        List<GenreEntity> genres = new() { new() { Id = 1 } };
        (Statistics service, _, _, _, Mock<IGenreRepository> genre) = Build(tracks, albums, artists, genres);

        // Act
        await service.UpdateGenresAsync(new[] { 1L });

        // Assert
        genre.Verify(r => r.UpdateStatisticsAsync(1, 2, 1, 1, 0, 1, 0, 300, RepositoryConnectionKind.Background), Times.Once);
    }

    [Fact(DisplayName = "UpdateGenresAsync should skip update when statistics already match")]
    public async Task UpdateGenresAsync_ShouldSkipUpdate_WhenStatisticsAlreadyMatch()
    {
        // Arrange
        List<GenreEntity> genres = new() { new() { Id = 1, TrackCount = 0, ArtistCount = 0, AlbumCount = 0 } };
        (Statistics service, _, _, _, Mock<IGenreRepository> genre) = Build(genres: genres);

        // Act
        await service.UpdateGenresAsync(new[] { 1L });

        // Assert
        genre.Verify(r => r.UpdateStatisticsAsync(
            It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<RepositoryConnectionKind>()),
            Times.Never);
    }
}