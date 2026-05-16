using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Playlists.Requests;

public class CreatePlaylistTracksRequest : IRequest<Unit>
{
    public long PlaylistId { get; set; }

    public List<CreatePlaylistTracksDto> Tracks { get; set; } = [];
}


public class CreatePlaylistTracksRequestHandler(IPlaylistTrackRepository _repository) : IRequestHandler<CreatePlaylistTracksRequest, Unit>
{
    public async Task<Unit> Handle(CreatePlaylistTracksRequest request, CancellationToken cancellationToken)
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

        return Unit.Value;
    }
}