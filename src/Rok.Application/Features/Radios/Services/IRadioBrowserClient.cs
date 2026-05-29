using Rok.Application.Dto;

namespace Rok.Application.Features.Radios.Services;

public interface IRadioBrowserClient
{
    Task<IReadOnlyList<RadioSearchResultDto>> SearchByNameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}
