using MiF.Mediator;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Command;

public class UpdatePlaylistPictureCommand : ICommand<Unit>
{
    [Required]
    public long Id { get; set; }

    public string Picture { get; set; } = string.Empty;
}


public class UpdatePlaylistPictureCommandHandler(IPlaylistHeaderRepository _repository) : ICommandHandler<UpdatePlaylistPictureCommand, Unit>
{
    public async Task<Unit> HandleAsync(UpdatePlaylistPictureCommand message, CancellationToken cancellationToken)
    {
        await _repository.UpdatePictureAsync(message.Id, message.Picture);

        return Unit.Result;
    }
}