using Rok.Application.Dto;

namespace Rok.Application.Interfaces;

public interface IEqualizerPresetResolver
{
    Task<EqualizerPresetDto?> ResolveAsync(TrackDto track);
}