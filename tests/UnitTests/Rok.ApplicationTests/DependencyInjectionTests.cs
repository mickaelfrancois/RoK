using Microsoft.Extensions.DependencyInjection;
using Rok.Application;
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
}