using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Albums.Requests;

public class GetAlbumListeningStatsRequest(long albumId) : IRequest<AlbumListeningStatsDto>
{
    public long AlbumId { get; } = albumId;
}

public sealed class GetAlbumListeningStatsRequestValidator : Validator<GetAlbumListeningStatsRequest>
{
    public GetAlbumListeningStatsRequestValidator()
    {
        Rule(x => x.AlbumId).GreaterThan(0L);
    }
}


public class GetAlbumListeningStatsRequestHandler(IListeningEventRepository listeningEventRepository) : IRequestHandler<GetAlbumListeningStatsRequest, AlbumListeningStatsDto>
{
    public Task<AlbumListeningStatsDto> Handle(GetAlbumListeningStatsRequest request, CancellationToken cancellationToken)
    {
        return listeningEventRepository.GetAlbumListeningStatsAsync(request.AlbumId);
    }
}
