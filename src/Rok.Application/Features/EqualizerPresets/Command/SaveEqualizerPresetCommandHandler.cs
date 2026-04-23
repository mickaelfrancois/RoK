using Rok.Application.Interfaces.Repositories;
using Rok.Domain.Enums;

namespace Rok.Application.Features.EqualizerPresets.Command;

public class SaveEqualizerPresetCommand : ICommand<Result<bool>>
{
    [Required]
    public EqualizerScope Scope { get; set; }

    public string? BuiltinPresetKey { get; set; }

    public long? ScopeId { get; set; }

    [Required]
    public float[] Bands { get; set; } = new float[10];
}

public class SaveEqualizerPresetCommandHandler(IEqualizerPresetRepository repository) : ICommandHandler<SaveEqualizerPresetCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(SaveEqualizerPresetCommand command, CancellationToken cancellationToken)
    {
        await repository.SaveAsync(command.ToEntity());

        return Result<bool>.Success(true);
    }
}