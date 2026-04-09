using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Query;

public class GetEqualizerPresetQuery(EqualizerScope scope, int? scopeId) : IQuery<Result<EqualizerPresetDto?>>
{
    public EqualizerScope Scope { get; } = scope;

    public int? ScopeId { get; } = scopeId;
}

public class GetEqualizerPresetQueryHandler(IEqualizerPresetRepository repository) : IQueryHandler<GetEqualizerPresetQuery, Result<EqualizerPresetDto?>>
{
    public async Task<Result<EqualizerPresetDto?>> HandleAsync(GetEqualizerPresetQuery query, CancellationToken cancellationToken)
    {
        EqualizerPresetEntity? entity = await repository.FindAsync(query.Scope, query.ScopeId);

        return Result<EqualizerPresetDto?>.Success(entity?.ToDto());
    }
}