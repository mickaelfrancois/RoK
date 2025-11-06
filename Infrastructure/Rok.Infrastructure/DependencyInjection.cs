using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rok.Application.Interfaces;
using Rok.Application.Tag;
using Rok.Infrastructure.Files;
using Rok.Infrastructure.FileSystem;
using Rok.Infrastructure.Lyrics;
using Rok.Infrastructure.Migration;
using Rok.Infrastructure.NovaApi;
using Rok.Infrastructure.Repositories;
using Rok.Infrastructure.Social;
using Rok.Infrastructure.Tag;
using Rok.Infrastructure.Telemetry;
using Serilog;
using Serilog.Events;
using Windows.Storage;

namespace Rok.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string applicationLocalPath)
    {
        string connectionString = "Data Source=database.sqlite;Mode=ReadWriteCreate;Cache=Shared;Pooling=True;";

        services.AddSingleton<IDbConnection>(c =>
        {
            return new SqliteConnection(connectionString);
        });

        services.AddKeyedSingleton<IDbConnection>("BackgroundConnection", new SqliteConnection(connectionString));


        services.AddSingleton<IFileSystem, DefaultFileSystem>();
        services.AddSingleton<ISettingsFile>(c => new SettingsFileService(applicationLocalPath));
        services.AddSingleton<IArtistPicture, ArtistPicture>();
        services.AddSingleton<IAlbumPicture, AlbumPicture>();
        services.AddSingleton<BackdropPicture>();

        services.AddSingleton<IPlayerEngine, WinUIMediaPlayer>();
        services.AddSingleton<ILyricsService, LyricsService>();
        services.AddSingleton<ILyricsParser, LyricsParser>();

        services.AddScoped<IAppDbContext, AppDbContext>();

        services.AddSingleton<IMigrationService, MigrationService>();
        services.AddSingleton<IMigration, Migration2>();

        services.AddScoped<IArtistRepository, ArtistRepository>();
        services.AddScoped<IAlbumRepository, AlbumRepository>();
        services.AddScoped<IGenreRepository, GenreRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<ICountryRepository, CountryRepository>();
        services.AddScoped<IPlaylistHeaderRepository, PlaylistHeaderRepository>();
        services.AddScoped<IPlaylistTrackRepository, PlaylistTrackRepository>();
        services.AddScoped<IPlaylistTrackGenerateRepository, PlaylistTrackGenerateRepository>();

        services.AddSingleton<ITagService, TagService>();
        services.AddSingleton<INovaApiService, NovaApiService>();
        services.AddSingleton<ITelemetryClient, TelemetryClient>();
        services.AddSingleton<DiscordRichPresenceService>();

        return services;
    }


    public static IServiceCollection AddLogger(this IServiceCollection services, string applicationLocalPath)
    {
        string logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs", "rok-.log");

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(
#if DEBUG
                Serilog.Events.LogEventLevel.Verbose
#else
                Serilog.Events.LogEventLevel.Information
#endif
            )
             .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 2,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddLogging(builder =>
        {
#if DEBUG
            LogLevel defaultMinLevel = LogLevel.Trace;
#else
            LogLevel defaultMinLevel = LogLevel.Information;
#endif
            builder.ClearProviders();
            builder.SetMinimumLevel(defaultMinLevel);

            builder.AddSerilog(dispose: true);
        });

        return services;
    }
}