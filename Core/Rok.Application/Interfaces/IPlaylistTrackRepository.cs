using Rok.Application.Features.Playlists.Query;

namespace Rok.Application.Interfaces;

public interface IPlaylistTrackGenerateRepository
{
    Task<List<TrackEntity>> GenerateAsync(GeneratePlaylistTracksQuery request, RepositoryConnectionKind kind = RepositoryConnectionKind.Foreground);
}
