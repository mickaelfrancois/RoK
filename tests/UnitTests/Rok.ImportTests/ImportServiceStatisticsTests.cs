using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Interfaces.Pictures;
using Rok.Application.Interfaces.Repositories;
using Rok.Application.Services;
using Rok.Import;
using Rok.Import.Services;

namespace Rok.ImportTests;

public class ImportServiceStatisticsTests
{
    private const long AlbumId = 1;

    private readonly Mock<IAlbumRepository> _albumRepository = new();
    private readonly ImportTrackingService _trackingService = new();
    private readonly List<TrackEntity> _tracks = [];
    private readonly ImportService _sut;

    public ImportServiceStatisticsTests()
    {
        Mock<IAppOptions> options = new();
        options.SetupGet(o => o.LibraryTokens).Returns(["token"]);

        Mock<IFolderResolver> folderResolver = new();
        folderResolver.Setup(f => f.GetPathFromTokenAsync(It.IsAny<string>())).ReturnsAsync([]);

        Mock<ITrackRepository> trackRepository = new();
        trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .Returns(() => Task.FromResult<IEnumerable<TrackEntity>>(_tracks.ToList()));

        AlbumEntity album = new() { Id = AlbumId, TrackCount = 0, Duration = 0 };

        _albumRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .Returns(() =>
            {
                _trackingService.AlbumUpdated(AlbumId);

                return Task.FromResult<IEnumerable<AlbumEntity>>([album]);
            });

        Mock<IArtistRepository> artistRepository = new();
        Mock<IGenreRepository> genreRepository = new();
        Mock<IMessenger> messenger = new();
        FakeTimeProvider timeProvider = new();

        TrackImport trackImport = new(trackRepository.Object);
        AlbumImport albumImport = new(_albumRepository.Object, timeProvider);
        ArtistImport artistImport = new(artistRepository.Object, timeProvider);
        GenreImport genreImport = new(genreRepository.Object, timeProvider);

        _sut = new ImportService(
            folderResolver.Object,
            options.Object,
            new Statistics(trackRepository.Object, _albumRepository.Object, artistRepository.Object, genreRepository.Object, NullLogger<Statistics>.Instance),
            trackImport,
            genreImport,
            albumImport,
            artistImport,
            Mock.Of<ICleanLibrary>(),
            Mock.Of<ITelemetryClient>(),
            new ImportProgressService(messenger.Object),
            _trackingService,
            new FileSystemService(NullLogger<FileSystemService>.Instance),
            (FolderImportProcessor)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(FolderImportProcessor)),
            new PostImportDominantColorTask(Mock.Of<IDominantColorCalculator>(), _albumRepository.Object, Mock.Of<IAlbumPictureService>(), artistRepository.Object, Mock.Of<IArtistPictureService>(), NullLogger<PostImportDominantColorTask>.Instance),
            new PostImportApiEnrichmentTask(artistImport, albumImport, Mock.Of<IArtistApiService>(), Mock.Of<IAlbumApiService>(), Mock.Of<IArtistPictureService>(), Mock.Of<IAlbumPictureService>(), Mock.Of<IBackdropPicture>(), new FakeMediator(), NullLogger<PostImportApiEnrichmentTask>.Instance),
            new ImportMessageThrottler(),
            new LibraryWriteGate(),
            NullLogger<ImportService>.Instance);
    }

    private static TrackEntity NewTrack(string file) => new() { MusicFile = file, AlbumId = AlbumId, Duration = 10 };

    [Fact(DisplayName = "when_the_library_changes_between_two_imports_the_second_import_updates_statistics_from_fresh_data")]
    public async Task ImportAsync_SecondImportAfterLibraryChange_UsesFreshStatistics()
    {
        // Arrange
        _tracks.Add(NewTrack("a.mp3"));
        await _sut.ImportAsync(CancellationToken.None);

        _tracks.Add(NewTrack("b.mp3"));

        // Act
        await _sut.ImportAsync(CancellationToken.None);

        // Assert
        _albumRepository.Verify(r => r.UpdateStatisticsAsync(AlbumId, 1, 10, RepositoryConnectionKind.Background), Times.Once);
        _albumRepository.Verify(r => r.UpdateStatisticsAsync(AlbumId, 2, 20, RepositoryConnectionKind.Background), Times.Once);
    }
}