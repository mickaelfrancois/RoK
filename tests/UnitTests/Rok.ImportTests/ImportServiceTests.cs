using System.Runtime.CompilerServices;
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

public class ImportServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly LibraryWriteGate _gate = new();
    private readonly Mock<ITrackRepository> _trackRepository = new();
    private readonly TaskCompletionSource _importStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _releaseImport = new(0, 1);
    private readonly ImportService _sut;

    public ImportServiceTests()
    {
        Mock<IAppOptions> options = new();
        options.SetupGet(o => o.LibraryTokens).Returns(["token"]);

        Mock<IFolderResolver> folderResolver = new();
        folderResolver.Setup(f => f.GetPathFromTokenAsync(It.IsAny<string>())).ReturnsAsync([]);

        _trackRepository.Setup(r => r.GetAllAsync(It.IsAny<RepositoryConnectionKind>()))
            .Returns(async () =>
            {
                _importStarted.TrySetResult();
                await _releaseImport.WaitAsync();

                return Enumerable.Empty<TrackEntity>();
            });

        Mock<IAlbumRepository> albumRepository = new();
        Mock<IArtistRepository> artistRepository = new();
        Mock<IGenreRepository> genreRepository = new();
        Mock<IMessenger> messenger = new();
        FakeTimeProvider timeProvider = new();

        TrackImport trackImport = new(_trackRepository.Object);
        AlbumImport albumImport = new(albumRepository.Object, timeProvider);
        ArtistImport artistImport = new(artistRepository.Object, timeProvider);
        GenreImport genreImport = new(genreRepository.Object, timeProvider);

        _sut = new ImportService(
            folderResolver.Object,
            options.Object,
            new Statistics(_trackRepository.Object, albumRepository.Object, artistRepository.Object, genreRepository.Object, NullLogger<Statistics>.Instance),
            trackImport,
            genreImport,
            albumImport,
            artistImport,
            Mock.Of<ICleanLibrary>(),
            Mock.Of<ITelemetryClient>(),
            new ImportProgressService(messenger.Object),
            new ImportTrackingService(),
            new FileSystemService(NullLogger<FileSystemService>.Instance),
            (FolderImportProcessor)RuntimeHelpers.GetUninitializedObject(typeof(FolderImportProcessor)),
            new PostImportDominantColorTask(Mock.Of<IDominantColorCalculator>(), albumRepository.Object, Mock.Of<IAlbumPictureService>(), artistRepository.Object, Mock.Of<IArtistPictureService>(), NullLogger<PostImportDominantColorTask>.Instance),
            new PostImportApiEnrichmentTask(artistImport, albumImport, Mock.Of<IArtistApiService>(), Mock.Of<IAlbumApiService>(), Mock.Of<IArtistPictureService>(), Mock.Of<IAlbumPictureService>(), Mock.Of<IBackdropPicture>(), new FakeMediator(), NullLogger<PostImportApiEnrichmentTask>.Instance),
            new ImportMessageThrottler(),
            _gate,
            NullLogger<ImportService>.Instance);
    }

    private async Task WaitUntilIdleAsync()
    {
        DateTime deadline = DateTime.UtcNow + Timeout;

        while (_sut.UpdateInProgress && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        Assert.False(_sut.UpdateInProgress);
    }

    [Fact(DisplayName = "when_a_write_holds_the_gate_the_import_waits_for_its_release")]
    public async Task Start_WriteHoldsGate_ImportWaitsForRelease()
    {
        // Arrange
        Assert.True(_gate.TryEnter(out IDisposable? lease));

        // Act
        _sut.Start(0);
        await Task.Delay(300);

        // Assert
        Assert.False(_importStarted.Task.IsCompleted);
        Assert.True(_sut.UpdateInProgress);

        lease.Dispose();
        _releaseImport.Release();

        await _importStarted.Task.WaitAsync(Timeout);
        await WaitUntilIdleAsync();
    }

    [Fact(DisplayName = "while_the_import_runs_it_holds_the_gate_and_releases_it_at_the_end")]
    public async Task Start_ImportRunning_HoldsGateUntilEnd()
    {
        // Act
        _sut.Start(0);
        await _importStarted.Task.WaitAsync(Timeout);

        // Assert
        Assert.False(_gate.TryEnter(out IDisposable? duringImport));
        Assert.Null(duringImport);

        _releaseImport.Release();
        await WaitUntilIdleAsync();

        Assert.True(_gate.TryEnter(out IDisposable? afterImport));
        afterImport.Dispose();
    }
}