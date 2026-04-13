using Rok.Domain.Enums;

namespace Rok.Application.Dto;

public class EqualizerPresetDto
{
    public long Id { get; set; }

    public EqualizerScope Scope { get; set; }

    public string? BuiltinPresetKey { get; set; }

    public long? ScopeId { get; set; }

    public float[] Bands { get; set; } = new float[10];
}