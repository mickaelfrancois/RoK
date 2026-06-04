using Rok.Application.Features.Albums;
using Rok.Application.Features.Insights;

namespace Rok.Application.Interfaces.Repositories;

public interface IListeningEventRepository : IRepository<ListeningEventEntity>
{
    Task<InsightsDto> GetInsightsAsync(DateTime month);

    Task<AlbumListeningStatsDto> GetAlbumListeningStatsAsync(long albumId);
}