namespace Rok.Application.Interfaces.Repositories;

public interface IRadioStationRepository
{
    Task<long> AddAsync(RadioStationEntity station, CancellationToken cancellationToken);

    Task<RadioStationEntity?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<RadioStationEntity?> GetByUrlAsync(string streamUrl, CancellationToken cancellationToken);

    Task<IReadOnlyList<RadioStationEntity>> ListAsync(CancellationToken cancellationToken);

    Task UpdateAsync(long id, string name, string streamUrl, string? homepageUrl, CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);

    Task TouchLastListenAsync(long id, DateTime utcNow, CancellationToken cancellationToken);
}