using Rok.Application.Features.ListeningEvents;
using Rok.Application.Interfaces.Repositories;

namespace Rok.Application.Features.Artists.Requests;

public class GetArtistListeningStatsRequest(long artistId) : IRequest<ListeningStatsDto>
{
    public long ArtistId { get; } = artistId;
}

public sealed class GetArtistListeningStatsRequestValidator : Validator<GetArtistListeningStatsRequest>
{
    public GetArtistListeningStatsRequestValidator()
    {
        Rule(x => x.ArtistId).GreaterThan(0L);
    }
}


public class GetArtistListeningStatsRequestHandler(IListeningEventRepository listeningEventRepository) : IRequestHandler<GetArtistListeningStatsRequest, ListeningStatsDto>
{
    public Task<ListeningStatsDto> Handle(GetArtistListeningStatsRequest request, CancellationToken cancellationToken)
    {
        return listeningEventRepository.GetArtistListeningStatsAsync(request.ArtistId);
    }
}
