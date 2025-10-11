using Microsoft.Extensions.DependencyInjection;
using Rok.Application.Interfaces;

namespace Rok.Import;

public static class DependencyInjection
{
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        services.AddSingleton<IImport, ImportService>();
        services.AddSingleton<ICleanLibrary, CleanLibraryService>();
        services.AddSingleton<Statistics>();


        services.AddSingleton<AlbumImport>();
        services.AddSingleton<ArtistImport>();
        services.AddSingleton<GenreImport>();
        services.AddSingleton<TrackImport>();

        services.AddSingleton<CountryCache>();

        return services;
    }
}
