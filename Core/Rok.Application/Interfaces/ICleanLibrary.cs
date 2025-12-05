namespace Rok.Application.Interfaces;

public interface ICleanLibrary
{
    Task CleanAsync(IEnumerable<long> trackIDReaded, ImportStatisticsDto statistics, CancellationToken cancellationToken);
}
