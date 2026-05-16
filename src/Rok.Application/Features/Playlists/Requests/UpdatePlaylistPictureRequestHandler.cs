using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class UpdatePlaylistPictureRequest : IRequest<Unit>
{
    public long Id { get; set; }

    public string Picture { get; set; } = string.Empty;
}

public sealed class UpdatePlaylistPictureRequestValidator : Validator<UpdatePlaylistPictureRequest>
{
    public UpdatePlaylistPictureRequestValidator() { Rule(x => x.Id).GreaterThan(0L); }
}

public class UpdatePlaylistPictureRequestHandler(IPlaylistHeaderRepository _repository) : IRequestHandler<UpdatePlaylistPictureRequest, Unit>
{
    public async Task<Unit> Handle(UpdatePlaylistPictureRequest message, CancellationToken cancellationToken)
    {
        await _repository.UpdatePictureAsync(message.Id, message.Picture);

        return Unit.Value;
    }
}