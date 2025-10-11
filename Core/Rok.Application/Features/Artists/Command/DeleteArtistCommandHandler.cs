using Rok.Application.Interfaces;

namespace Rok.Application.Features.Artists.Command;

public class DeleteArtistCommand : ICommand<Result<bool>>
{
    [RequiredGreaterThanZero]
    public long Id { get; set; }
}

public class DeleteArtistCommandHandler(IArtistRepository _artistRepository) : ICommandHandler<DeleteArtistCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(DeleteArtistCommand message, CancellationToken cancellationToken)
    {
        bool result = await _artistRepository.DeleteAsync(new ArtistEntity { Id = message.Id });

        if (result)
            return Result<bool>.Success(true);
        else
            return Result<bool>.Fail("Failed to delete artist.");
    }
}
