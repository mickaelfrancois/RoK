using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Requests;

public class DeleteEqualizerPresetRequest : IRequest<Result<bool>>
{
    public EqualizerScope Scope { get; set; }

    public long? ScopeId { get; set; }
}

public class DeleteEqualizerPresetRequestHandler(IEqualizerPresetRepository repository) : IRequestHandler<DeleteEqualizerPresetRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteEqualizerPresetRequest message, CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(message.Scope, message.ScopeId);

        return Result<bool>.Ok(true);
    }
}