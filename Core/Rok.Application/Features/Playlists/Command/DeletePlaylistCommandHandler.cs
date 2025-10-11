using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Command;

public class DeletePlaylistCommand : ICommand<Result<bool>>
{
    [Required]
    public long Id { get; set; }
}

public class DeletePlaylistCommandHandler(IPlaylistHeaderRepository _repository) : ICommandHandler<DeletePlaylistCommand, Result<bool>>
{
    public async Task<Result<bool>> HandleAsync(DeletePlaylistCommand message, CancellationToken cancellationToken)
    {
        int deleteRows = await _repository.DeleteAsync(message.Id);

        if (deleteRows == 1)
            return Result<bool>.Success(true);
        else
            return Result<bool>.Fail("Failed to delete playlist.");
    }
}
