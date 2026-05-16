using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class DeletePlaylistRequest : IRequest<Result<bool>>
{
    public long Id { get; set; }
}

public sealed class DeletePlaylistRequestValidator : Validator<DeletePlaylistRequest>
{
    public DeletePlaylistRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class DeletePlaylistRequestHandler(IPlaylistHeaderRepository _repository) : IRequestHandler<DeletePlaylistRequest, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeletePlaylistRequest message, CancellationToken cancellationToken)
    {
        int deleteRows = await _repository.DeleteAsync(message.Id);

        if (deleteRows == 1)
            return Result<bool>.Ok(true);
        else
            return Result<bool>.Fail(new OperationError("playlist.delete_failed", "Failed to delete playlist."));
    }
}
