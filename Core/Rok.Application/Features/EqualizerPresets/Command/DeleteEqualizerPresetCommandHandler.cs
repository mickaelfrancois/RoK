using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Command;

public class DeleteEqualizerPresetCommand : ICommand<Result<bool>>
{
    [Required]
    public EqualizerScope Scope { get; set; }

    public long? ScopeId { get; set; }
}

public class DeleteEqualizerPresetCommandHandler(IEqualizerPresetRepository repository) : ICommandHandler<DeleteEqualizerPresetCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(DeleteEqualizerPresetCommand command, CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(command.Scope, command.ScopeId);

        return Result<bool>.Success(true);
    }
}
