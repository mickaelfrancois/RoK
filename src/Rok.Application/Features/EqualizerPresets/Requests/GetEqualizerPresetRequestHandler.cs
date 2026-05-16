using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Requests;

public class GetEqualizerPresetRequest(EqualizerScope scope, int? scopeId) : IRequest<Result<EqualizerPresetDto>>
{
    public EqualizerScope Scope { get; } = scope;

    public int? ScopeId { get; } = scopeId;
}

public class GetEqualizerPresetRequestHandler(IEqualizerPresetRepository repository) : IRequestHandler<GetEqualizerPresetRequest, Result<EqualizerPresetDto>>
{
    public async Task<Result<EqualizerPresetDto>> Handle(GetEqualizerPresetRequest message, CancellationToken cancellationToken)
    {
        EqualizerPresetEntity? entity = await repository.FindAsync(message.Scope, message.ScopeId);

        if (entity == null)
            return Result<EqualizerPresetDto>.Fail(NotFoundError.ForEntity("EqualizerPreset", message.Scope));

        return Result<EqualizerPresetDto>.Ok(entity.ToDto());
    }
}
