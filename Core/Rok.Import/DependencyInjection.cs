using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Interfaces;
using Rok.Import.Services;

namespace Rok.Import;

public static class DependencyInjection
{
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.AddSingleton<ICleanLibrary, CleanLibraryService>();
        services.AddSingleton<Statistics>();


        services.AddSingleton<AlbumImport>();
        services.AddSingleton<ArtistImport>();
        services.AddSingleton<GenreImport>();
        services.AddSingleton<TrackImport>();

        services.AddSingleton<CountryCache>();
        services.AddSingleton<ImportMessageThrottler>();

        services.AddScoped<ImportProgressService>();
        services.AddScoped<ImportTrackingService>();
        services.AddScoped<FileSystemService>();
        services.AddScoped<TrackFileProcessor>();
        services.AddScoped<TrackMetadataService>();
        services.AddScoped<FolderImportProcessor>();
        services.AddScoped<IImport, ImportService>();

        return services;
    }
}
