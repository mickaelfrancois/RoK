using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Validation;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.EqualizerPresets;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Application.Pipeline;
using Rok.Application.Player;
using Rok.Application.Services;

namespace Rok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppOptions, AppOptions>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<IReviewPromptEligibilityService, ReviewPromptEligibilityService>();
        services.AddSingleton<IEqualizerPresetResolver, EqualizerPresetResolver>();

        services.AddSingleton<IPlayerSleepModeService, PlayerSleepModeService>();

        services.AddTransient<TrackLyricsService>();
        services.AddTransient<IArtistApiService, ArtistApiService>();
        services.AddTransient<IAlbumApiService, AlbumApiService>();

        services.AddMediator();
        services.AddValidators();
        services.AddValidationBehavior();

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));

        return services;
    }
}
