using Moq;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Entities;
using Rok.MetadataTool;

namespace Rok.ApplicationTests.Tools;

public class DryRunTrackAnalysisRepositoryTests
{
    private readonly Mock<ITrackAnalysisRepository> _inner = new();

    [Fact(DisplayName = "dry_run_repository_never_writes_and_serves_its_own_rows")]
    public async Task UpsertAsync_NeverWritesToInnerAndServesItsOwnRow()
    {
        // Arrange
        var stored = new TrackAnalysisEntity { TrackId = 2, FileSize = 1 };
        var written = new TrackAnalysisEntity { TrackId = 1, FileSize = 2 };
        _inner.Setup(r => r.GetAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        _inner.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((TrackAnalysisEntity?)null);
        var repository = new DryRunTrackAnalysisRepository(_inner.Object);

        // Act
        await repository.UpsertAsync(written, CancellationToken.None);
        var own = await repository.GetAsync(1, CancellationToken.None);
        var fromInner = await repository.GetAsync(2, CancellationToken.None);

        // Assert
        Assert.Same(written, own);
        Assert.Same(stored, fromInner);
        _inner.Verify(r => r.UpsertAsync(It.IsAny<TrackAnalysisEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "in_memory_row_hides_the_inner_row_of_the_same_track")]
    public async Task GetAsync_InMemoryRow_WinsOverInner()
    {
        // Arrange
        var written = new TrackAnalysisEntity { TrackId = 1, FileSize = 2 };
        _inner.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new TrackAnalysisEntity { TrackId = 1, FileSize = 1 });
        var repository = new DryRunTrackAnalysisRepository(_inner.Object);

        // Act
        await repository.UpsertAsync(written, CancellationToken.None);
        var row = await repository.GetAsync(1, CancellationToken.None);

        // Assert
        Assert.Same(written, row);
    }
}