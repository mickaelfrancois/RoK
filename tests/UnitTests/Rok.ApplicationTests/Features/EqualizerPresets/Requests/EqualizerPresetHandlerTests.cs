using Moq;
using Rok.Application.Features.EqualizerPresets;
using Rok.Application.Features.EqualizerPresets.Requests;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.ApplicationTests.Features.EqualizerPresets.Requests;

public class SaveEqualizerPresetRequestHandlerTests
{
    [Fact(DisplayName = "Handle should save preset and return success")]
    public async Task Handle_ShouldSavePreset_AndReturnSuccess()
    {
        // Arrange
        Mock<IEqualizerPresetRepository> repository = new();
        SaveEqualizerPresetRequestHandler handler = new(repository.Object);
        float[] bands = new float[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        SaveEqualizerPresetRequest request = new() { Scope = EqualizerScope.Track, ScopeId = 42, Bands = bands };

        // Act
        Result<bool> result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        repository.Verify(r => r.SaveAsync(It.Is<EqualizerPresetEntity>(e => e.Scope == EqualizerScope.Track && e.ScopeId == 42 && e.Bands == bands)), Times.Once);
    }
}

public class DeleteEqualizerPresetRequestHandlerTests
{
    [Fact(DisplayName = "Handle should delete preset for provided scope and scope id")]
    public async Task Handle_ShouldDeletePreset_ForProvidedScopeAndScopeId()
    {
        // Arrange
        Mock<IEqualizerPresetRepository> repository = new();
        DeleteEqualizerPresetRequestHandler handler = new(repository.Object);

        // Act
        Result<bool> result = await handler.Handle(new DeleteEqualizerPresetRequest { Scope = EqualizerScope.Album, ScopeId = 5 }, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        repository.Verify(r => r.DeleteAsync(EqualizerScope.Album, 5L), Times.Once);
    }
}

public class GetEqualizerPresetRequestHandlerTests
{
    [Fact(DisplayName = "Handle should return mapped preset when repository finds one")]
    public async Task Handle_ShouldReturnMappedPreset_WhenRepositoryFindsOne()
    {
        // Arrange
        EqualizerPresetEntity entity = new() { Id = 9, Scope = EqualizerScope.Artist, ScopeId = 3, Bands = new float[10] };
        Mock<IEqualizerPresetRepository> repository = new();
        repository.Setup(r => r.FindAsync(EqualizerScope.Artist, 3L)).ReturnsAsync(entity);
        GetEqualizerPresetRequestHandler handler = new(repository.Object);

        // Act
        Result<EqualizerPresetDto> result = await handler.Handle(new GetEqualizerPresetRequest(EqualizerScope.Artist, 3), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(EqualizerScope.Artist, result.Value!.Scope);
    }

    [Fact(DisplayName = "Handle should return failure when no preset is found")]
    public async Task Handle_ShouldReturnFailure_WhenNoPresetIsFound()
    {
        // Arrange
        Mock<IEqualizerPresetRepository> repository = new();
        repository.Setup(r => r.FindAsync(It.IsAny<EqualizerScope>(), It.IsAny<long?>())).ReturnsAsync((EqualizerPresetEntity?)null);
        GetEqualizerPresetRequestHandler handler = new(repository.Object);

        // Act
        Result<EqualizerPresetDto> result = await handler.Handle(new GetEqualizerPresetRequest(EqualizerScope.Default, null), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
    }
}

public class EqualizerPresetResolverTests
{
    [Fact(DisplayName = "Resolve should prefer track-scoped preset over any other scope")]
    public async Task Resolve_ShouldPreferTrackScopedPreset_OverAnyOtherScope()
    {
        // Arrange
        EqualizerPresetEntity trackPreset = new() { Scope = EqualizerScope.Track, ScopeId = 1 };
        Mock<IEqualizerPresetRepository> repository = new();
        repository.Setup(r => r.FindAsync(EqualizerScope.Track, 1L)).ReturnsAsync(trackPreset);
        EqualizerPresetResolver resolver = new(repository.Object);

        // Act
        EqualizerPresetDto? result = await resolver.ResolveAsync(new TrackDto { Id = 1, AlbumId = 2, ArtistId = 3, GenreId = 4 });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EqualizerScope.Track, result!.Scope);
        repository.Verify(r => r.FindAsync(EqualizerScope.Album, It.IsAny<long?>()), Times.Never);
    }

    [Fact(DisplayName = "Resolve should fall back through album artist genre and finally default")]
    public async Task Resolve_ShouldFallBack_ThroughAlbumArtistGenre_AndFinallyDefault()
    {
        // Arrange
        EqualizerPresetEntity defaultPreset = new() { Scope = EqualizerScope.Default };
        Mock<IEqualizerPresetRepository> repository = new();
        repository.Setup(r => r.FindAsync(It.IsAny<EqualizerScope>(), It.IsAny<long?>())).ReturnsAsync((EqualizerPresetEntity?)null);
        repository.Setup(r => r.FindAsync(EqualizerScope.Default, null)).ReturnsAsync(defaultPreset);
        EqualizerPresetResolver resolver = new(repository.Object);

        // Act
        EqualizerPresetDto? result = await resolver.ResolveAsync(new TrackDto { Id = 1, AlbumId = 2, ArtistId = 3, GenreId = 4 });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(EqualizerScope.Default, result!.Scope);
    }

    [Fact(DisplayName = "Resolve should skip scopes whose scope id is null")]
    public async Task Resolve_ShouldSkipScopes_WhoseScopeIdIsNull()
    {
        // Arrange
        Mock<IEqualizerPresetRepository> repository = new();
        repository.Setup(r => r.FindAsync(It.IsAny<EqualizerScope>(), It.IsAny<long?>())).ReturnsAsync((EqualizerPresetEntity?)null);
        EqualizerPresetResolver resolver = new(repository.Object);

        // Act
        EqualizerPresetDto? result = await resolver.ResolveAsync(new TrackDto { Id = 1, AlbumId = null, ArtistId = null, GenreId = null });

        // Assert
        Assert.Null(result);
        repository.Verify(r => r.FindAsync(EqualizerScope.Album, It.IsAny<long?>()), Times.Never);
        repository.Verify(r => r.FindAsync(EqualizerScope.Artist, It.IsAny<long?>()), Times.Never);
        repository.Verify(r => r.FindAsync(EqualizerScope.Genre, It.IsAny<long?>()), Times.Never);
        repository.Verify(r => r.FindAsync(EqualizerScope.Default, null), Times.Once);
    }
}
