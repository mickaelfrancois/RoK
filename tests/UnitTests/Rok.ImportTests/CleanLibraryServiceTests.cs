using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Import;

namespace Rok.ImportTests;

public class CleanLibraryServiceTests
{
    private static (CleanLibraryService service, Mock<ITrackRepository> track, Mock<IAlbumRepository> album, Mock<IArtistRepository> artist, Mock<IGenreRepository> genre) Build(
        IEnumerable<TrackEntity>? tracks = null,
        int albumOrphans = 0,
        int artistOrphans = 0,
        int genreOrphans = 0)
    {
        Mock<ITrackRepository> trackRepo = new();
        trackRepo.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(tracks ?? Array.Empty<TrackEntity>());
        trackRepo.Setup(r => r.DeleteAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(true);
        Mock<IAlbumRepository> albumRepo = new();
        albumRepo.Setup(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(albumOrphans);
        Mock<IArtistRepository> artistRepo = new();
        artistRepo.Setup(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(artistOrphans);
        Mock<IGenreRepository> genreRepo = new();
        genreRepo.Setup(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>())).ReturnsAsync(genreOrphans);
        CleanLibraryService service = new(trackRepo.Object, artistRepo.Object, albumRepo.Object, genreRepo.Object, NullLogger<CleanLibraryService>.Instance);
        return (service, trackRepo, albumRepo, artistRepo, genreRepo);
    }

    [Fact(DisplayName = "Clean should delete tracks whose ids are not in the readed set")]
    public async Task Clean_ShouldDeleteTracks_WhoseIdsAreNotInReadedSet()
    {
        // Arrange
        List<TrackEntity> tracks = new()
        {
            new() { Id = 1 },
            new() { Id = 2 },
            new() { Id = 3 }
        };
        (CleanLibraryService service, Mock<ITrackRepository> trackRepo, _, _, _) = Build(tracks);
        ImportStatisticsDto stats = new();

        // Act
        await service.CleanAsync(new[] { 1L, 3L }, stats, CancellationToken.None);

        // Assert
        trackRepo.Verify(r => r.DeleteAsync(It.Is<TrackEntity>(t => t.Id == 2), It.IsAny<RepositoryConnectionKind>()), Times.Once);
        trackRepo.Verify(r => r.DeleteAsync(It.Is<TrackEntity>(t => t.Id == 1), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        trackRepo.Verify(r => r.DeleteAsync(It.Is<TrackEntity>(t => t.Id == 3), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        Assert.Equal(1, stats.TracksDeleted);
    }

    [Fact(DisplayName = "Clean should skip track deletion step when every track is still in library")]
    public async Task Clean_ShouldSkipTrackDeletionStep_WhenEveryTrackIsStillInLibrary()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1 }, new() { Id = 2 } };
        (CleanLibraryService service, Mock<ITrackRepository> trackRepo, _, _, _) = Build(tracks);
        ImportStatisticsDto stats = new();

        // Act
        await service.CleanAsync(new[] { 1L, 2L }, stats, CancellationToken.None);

        // Assert
        trackRepo.Verify(r => r.DeleteAsync(It.IsAny<TrackEntity>(), It.IsAny<RepositoryConnectionKind>()), Times.Never);
        Assert.Equal(0, stats.TracksDeleted);
    }

    [Fact(DisplayName = "Clean should aggregate orphan deletion counts into the statistics DTO")]
    public async Task Clean_ShouldAggregateOrphanDeletionCounts_IntoStatisticsDto()
    {
        // Arrange
        (CleanLibraryService service, _, Mock<IAlbumRepository> albumRepo, Mock<IArtistRepository> artistRepo, Mock<IGenreRepository> genreRepo) = Build(albumOrphans: 3, artistOrphans: 2, genreOrphans: 4);
        ImportStatisticsDto stats = new();

        // Act
        await service.CleanAsync(Array.Empty<long>(), stats, CancellationToken.None);

        // Assert
        Assert.Equal(3, stats.AlbumsDeleted);
        Assert.Equal(2, stats.ArtistsDeleted);
        Assert.Equal(4, stats.GenresDeleted);
        albumRepo.Verify(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>()), Times.Once);
        artistRepo.Verify(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>()), Times.Once);
        genreRepo.Verify(r => r.DeleteOrphansAsync(It.IsAny<RepositoryConnectionKind>()), Times.Once);
    }

    [Fact(DisplayName = "Clean should swallow a cancellation during track deletion without throwing")]
    public async Task Clean_ShouldSwallowCancellation_DuringTrackDeletion_WithoutThrowing()
    {
        // Arrange
        List<TrackEntity> tracks = new() { new() { Id = 1 }, new() { Id = 2 } };
        (CleanLibraryService service, _, _, _, _) = Build(tracks);
        ImportStatisticsDto stats = new();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act
        await service.CleanAsync(Array.Empty<long>(), stats, cts.Token);

        // Assert
        Assert.Equal(0, stats.TracksDeleted);
    }

    [Fact(DisplayName = "Clean should add to existing statistics values instead of replacing them")]
    public async Task Clean_ShouldAddToExistingStatisticsValues_InsteadOfReplacingThem()
    {
        // Arrange
        (CleanLibraryService service, _, _, _, _) = Build(albumOrphans: 1, artistOrphans: 1, genreOrphans: 1);
        ImportStatisticsDto stats = new() { AlbumsDeleted = 10, ArtistsDeleted = 5, GenresDeleted = 2 };

        // Act
        await service.CleanAsync(Array.Empty<long>(), stats, CancellationToken.None);

        // Assert
        Assert.Equal(11, stats.AlbumsDeleted);
        Assert.Equal(6, stats.ArtistsDeleted);
        Assert.Equal(3, stats.GenresDeleted);
    }
}
