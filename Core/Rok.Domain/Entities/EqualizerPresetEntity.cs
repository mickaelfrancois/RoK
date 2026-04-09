using Rok.Domain.Enums;

namespace Rok.Domain.Entities;

[Table("EqualizerPreset")]
public class EqualizerPresetEntity
{
    public long Id { get; init; }

    public EqualizerScope Scope { get; init; }

    public long? ScopeId { get; init; }

    public float[] Bands { get; init; } = new float[10];
}
