using Rok.Application.Features.Playlists.Requests;

namespace Rok.Application.Interfaces.Repositories;

public interface IPlaylistTrackGenerateRepository
{
    Task<List<TrackEntity>> GenerateAsync(GeneratePlaylistTracksRequest request, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}