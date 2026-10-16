using Microsoft.Extensions.DependencyInjection;
using Rok.Application;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Requests;
using Rok.Application.Player.Mix;

namespace Rok.ApplicationTests;

public class DependencyInjectionTests
{
    [Fact(DisplayName = "mix_cue_provider_is_registered")]
    public void AddApplication_RegistersMixCueProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IMixCueProvider));
        Assert.Equal(typeof(MixAnalysisService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_rename_service_is_registered")]
    public void AddApplication_RegistersArtistRenameService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ArtistRenameService));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    [Fact(DisplayName = "artist_rename_handlers_are_registered")]
    public void AddApplication_RegistersArtistRenameHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<RenameArtistRequest, Result<ArtistRenameOutcome>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<GetArtistRenamePreviewRequest, Result<ArtistRenamePreview>>));
    }
}