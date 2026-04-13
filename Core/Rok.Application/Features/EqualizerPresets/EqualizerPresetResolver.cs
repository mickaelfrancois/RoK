using Rok.Application.Interfaces;
using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets;

public class EqualizerPresetResolver(IEqualizerPresetRepository repository) : IEqualizerPresetResolver
{
    public async Task<EqualizerPresetDto?> ResolveAsync(TrackDto track)
    {
        return
            await FindAsync(EqualizerScope.Track, track.Id)
            ?? await FindAsync(EqualizerScope.Album, track.AlbumId)
            ?? await FindAsync(EqualizerScope.Artist, track.ArtistId)
            ?? await FindAsync(EqualizerScope.Genre, track.GenreId)
            ?? await FindAsync(EqualizerScope.Default, null);
    }

    private async Task<EqualizerPresetDto?> FindAsync(EqualizerScope scope, long? scopeId)
    {
        if (scope != EqualizerScope.Default && scopeId is null)
            return null;

        EqualizerPresetEntity? entity = await repository.FindAsync(scope, scopeId);
        return entity?.ToDto();
    }
}
