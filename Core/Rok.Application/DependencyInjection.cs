using Microsoft.Extensions.DependencyInjection;
using MiF.Mediator;
using MiF.Mediator.DependencyInjection;
using Rok.Application.Features.Playlists;
using Rok.Application.Interfaces;
using Rok.Application.Options;

namespace Rok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppOptions, AppOptions>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();

        services.AddSimpleMediator();

        return services;
    }
}