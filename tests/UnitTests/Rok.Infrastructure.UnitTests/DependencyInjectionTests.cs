using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Infrastructure.Migration;

namespace Rok.Infrastructure.UnitTests;

public class DependencyInjectionTests
{
    private static ServiceCollection Register()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Path.GetTempPath());

        return services;
    }

    [Fact(DisplayName = "migration_19_is_registered")]
    public void AddInfrastructure_RegistersMigration19()
    {
        // Act
        var services = Register();

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IMigration) && d.ImplementationType == typeof(Migration19));
    }

    [Fact(DisplayName = "every_migration_target_version_is_unique")]
    public void AddInfrastructure_MigrationVersionsAreUnique()
    {
        // Act
        var services = Register();

        // Assert
        var versions = services
            .Where(d => d.ServiceType == typeof(IMigration) && d.ImplementationType is not null)
            .Select(d => ((IMigration)Activator.CreateInstance(d.ImplementationType!)!).TargetVersion)
            .ToList();

        Assert.Equal(versions.Count, versions.Distinct().Count());
        Assert.Contains(19, versions);
    }

    [Fact(DisplayName = "track_analysis_repository_is_registered_as_singleton")]
    public void AddInfrastructure_RegistersTrackAnalysisRepositoryAsSingleton()
    {
        // Act
        var services = Register();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ITrackAnalysisRepository));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact(DisplayName = "power_state_provider_is_registered_as_singleton")]
    public void AddInfrastructure_RegistersPowerStateProviderAsSingleton()
    {
        // Act
        var services = Register();

        // Assert
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPowerStateProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}