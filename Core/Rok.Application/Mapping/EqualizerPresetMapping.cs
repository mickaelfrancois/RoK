using Rok.Application.Features.EqualizerPresets.Command;

namespace Rok.Application.Mapping;

public static class EqualizerPresetMapping
{
    public static EqualizerPresetDto ToDto(this EqualizerPresetEntity entity) => new()
    {
        Id = entity.Id,
        Scope = entity.Scope,
        ScopeId = entity.ScopeId,
        Bands = entity.Bands
    };

    public static EqualizerPresetEntity ToEntity(this SaveEqualizerPresetCommand command) => new()
    {
        Scope = command.Scope,
        ScopeId = command.ScopeId,
        Bands = command.Bands
    };
}