using CleanArch.DevKit.Mediator;
using CleanArch.DevKit.Mediator.Behaviors;
using CleanArch.DevKit.Mediator.Results;
using CleanArch.DevKit.Mediator.Validation;
using CleanArch.DevKit.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Features.Albums.Edit;
using Rok.Application.Features.Albums.Merge;
using Rok.Application.Features.Albums.Services;
using Rok.Application.Features.Artists.Merge;
using Rok.Application.Features.Artists.Rename;
using Rok.Application.Features.Artists.Services;
using Rok.Application.Features.EqualizerPresets;
using Rok.Application.Features.Playlists;
using Rok.Application.Features.Tags;
using Rok.Application.Features.Tracks.Services;
using Rok.Application.Interfaces;
using Rok.Application.Options;
using Rok.Application.Player;
using Rok.Application.Player.Mix;
using Rok.Application.Player.Output;
using Rok.Application.Services;

namespace Rok.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAppOptions, AppOptions>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IPlayerService, PlayerService>();
        services.AddSingleton<AudioOutputCoordinator>();
        services.AddSingleton<IMixCueProvider, MixAnalysisService>();
        services.AddSingleton<IReviewPromptEligibilityService, ReviewPromptEligibilityService>();
        services.AddSingleton<IReviewPromptService, ReviewPromptService>();
        services.AddSingleton<IEqualizerPresetResolver, EqualizerPresetResolver>();
        services.AddSingleton<ILibraryWriteGate, LibraryWriteGate>();

        services.AddSingleton<IPlayerSleepModeService, PlayerSleepModeService>();

        services.AddTransient<TrackLyricsService>();
        services.AddTransient<TrackTagEditService>();
        services.AddTransient<TrackFileTagWriter>();
        services.AddTransient<ArtistFileRewriter>();
        services.AddTransient<ArtistRenameService>();
        services.AddTransient<ArtistMergeService>();
        services.AddTransient<AlbumEditService>();
        services.AddTransient<AlbumMergeService>();
        services.AddTransient<IArtistApiService, ArtistApiService>();
        services.AddTransient<IAlbumApiService, AlbumApiService>();

        services.AddMediator();
        services.AddValidators();
        services.AddValidationBehavior();
        services.AddResultBehavior();
        services.AddMessenger();
        services.AddCommonBehaviors();

        return services;
    }
}