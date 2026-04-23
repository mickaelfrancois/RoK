using Rok.Application.Features.EqualizerPresets.Command;

namespace Rok.Application.Mapping;

public static class EqualizerPresetMapping
{
    public static EqualizerPresetDto ToDto(this EqualizerPresetEntity entity) => new()
    {
        Id = entity.Id,
        Scope = entity.Scope,
        BuiltinPresetKey = entity.BuiltinPresetKey,
        ScopeId = entity.ScopeId,
        Bands = entity.Bands
    };

    public static EqualizerPresetEntity ToEntity(this SaveEqualizerPresetCommand command) => new()
    {
        Scope = command.Scope,
        BuiltinPresetKey = command.BuiltinPresetKey,
        ScopeId = command.ScopeId,
        Bands = command.Bands
    };
}