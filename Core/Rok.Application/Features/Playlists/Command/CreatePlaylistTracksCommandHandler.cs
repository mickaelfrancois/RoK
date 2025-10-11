using MiF.Mediator;
using Rok.Application.Interfaces;

namespace Rok.Application.Features.Playlists.Command;

public class CreatePlaylistTracksCommand : ICommand<Unit>
{
    public long PlaylistId { get; set; }

    public List<CreatePlaylistTracksDto> Tracks { get; set; } = [];
}


public class CreatePlaylistTracksCommandHandler(IPlaylistTrackRepository _repository) : ICommandHandler<CreatePlaylistTracksCommand, Unit>
{
    public async Task<Unit> HandleAsync(CreatePlaylistTracksCommand request, CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(request.PlaylistId);

        foreach (CreatePlaylistTracksDto track in request.Tracks)
        {
            PlaylistTrackEntity trackEntity = new()
            {
                PlaylistId = request.PlaylistId,
                TrackId = track.TrackId,
                Position = track.Position,
                Listened = track.Listened
            };
            await _repository.AddAsync(trackEntity);
        }

        return Unit.Result;
    }
}
