using Microsoft.Extensions.DependencyInjection;
using MiF.Mediator;
using MiF.Mediator.DependencyInjection;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.EqualizerPresets;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Services;

namespace Rok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppOptions, AppOptions>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IReviewPromptEligibilityService, ReviewPromptEligibilityService>();
        services.AddSingleton<IEqualizerPresetResolver, EqualizerPresetResolver>();

        services.AddSingleton<IPlayerSleepModeService, PlayerSleepModeService>();

        services.AddTransient<TrackLyricsService>();
        services.AddTransient<AlbumApiService>();
        services.AddTransient<ArtistApiService>();

        services.AddSimpleMediator();

        return services;
    }
}