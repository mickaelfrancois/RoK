using Rok.Domain.Enums;

namespace Rok.Application.Interfaces.Repositories;

public interface IEqualizerPresetRepository
{
    Task<EqualizerPresetEntity?> FindAsync(EqualizerScope scope, long? scopeId);

    Task SaveAsync(EqualizerPresetEntity preset);

    Task DeleteAsync(EqualizerScope scope, long? scopeId);
}