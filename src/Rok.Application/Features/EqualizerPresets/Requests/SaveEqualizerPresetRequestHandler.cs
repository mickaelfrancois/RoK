using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Requests;

public class SaveEqualizerPresetRequest : IRequest<Result<bool>>
{
    public EqualizerScope Scope { get; set; }

    public string? BuiltinPresetKey { get; set; }

    public long? ScopeId { get; set; }

    public float[] Bands { get; set; } = new float[10];
}

public sealed class SaveEqualizerPresetRequestValidator : Validator<SaveEqualizerPresetRequest>
{
    public SaveEqualizerPresetRequestValidator()
    {
        Rule(x => x.Bands).NotNull();
    }
}

public class SaveEqualizerPresetRequestHandler(IEqualizerPresetRepository repository) : IRequestHandler<SaveEqualizerPresetRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(SaveEqualizerPresetRequest message, CancellationToken cancellationToken)
    {
        await repository.SaveAsync(message.ToEntity());

        return Result<bool>.Ok(true);
    }
}
