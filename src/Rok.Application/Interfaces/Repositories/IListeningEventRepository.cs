using Rok.Application.Features.Insights;
using Rok.Application.Features.ListeningEvents;

namespace Rok.Application.Interfaces.Repositories;

public interface IListeningEventRepository : IRepository<ListeningEventEntity>
{
    Task<InsightsDto> GetInsightsAsync(DateTime month);

    Task<ListeningStatsDto> GetAlbumListeningStatsAsync(long albumId);

    Task<ListeningStatsDto> GetArtistListeningStatsAsync(long artistId);
}